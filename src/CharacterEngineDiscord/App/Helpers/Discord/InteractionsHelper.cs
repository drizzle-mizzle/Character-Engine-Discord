using System.Text.RegularExpressions;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.IntegraionModules;
using CharacterEngineDiscord.Helpers.Common;
using CharacterEngineDiscord.Helpers.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.WebSocket;
using NLog;
using SakuraAi.Client.Exceptions;
using SakuraAi.Client.Models.Common;
using DI = CharacterEngine.App.Helpers.Infrastructure.DependencyInjectionHelper;

namespace CharacterEngine.App.Helpers.Discord;


public static class InteractionsHelper
{
    private static ILogger _log = DI.GetLogger;


    private static readonly Regex DISCORD_REGEX = new("discord", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);


    public static SlashCommandProperties BuildStartCommand()
        => new SlashCommandBuilder().WithName("start").WithDescription("Register bot commands").WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild).Build();

    public static SlashCommandProperties BuildDisableCommand()
        => new SlashCommandBuilder().WithName("disable").WithDescription("Unregister all bot commands").WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild).Build();


    public static string NewCustomId(ModalActionType action, string data)
        => NewCustomId(Guid.NewGuid(), action, data);

    public static string NewCustomId(ModalData modalData)
        => NewCustomId(modalData.Id, modalData.ActionType, modalData.Data);

    public static string NewCustomId(Guid id, ModalActionType action, string data)
        => $"{id}{CommonHelper.COMMAND_SEPARATOR}{action}{CommonHelper.COMMAND_SEPARATOR}{data}";


    public static ModalData ParseCustomId(string customId)
    {
        var parts = customId.Split(CommonHelper.COMMAND_SEPARATOR);
        return new ModalData(Guid.Parse(parts[0]), Enum.Parse<ModalActionType>(parts[1]), parts[2]);
    }


    public static Embed BuildSearchResultList(SearchQuery searchQuery)
    {
        var type = searchQuery.IntegrationType;
        var embed = new EmbedBuilder().WithColor(type.GetColor());

        var title = $"{type.GetIcon()} {type:G}";
        var listTitle = $"({searchQuery.Characters.Count}) Characters found by query **\"{searchQuery.OriginalQuery}\"**:";
        embed.AddField(title, listTitle);

        var rows = Math.Min(searchQuery.Characters.Count, 10);
        var pageMultiplier = (searchQuery.CurrentPage - 1) * 10;

        for (var row = 1; row <= rows; row++)
        {
            var characterNumber = row + pageMultiplier;
            var character = searchQuery.Characters.ElementAt(characterNumber - 1);

            var rowTitle = $"{characterNumber}. {character.CharacterName}";
            var rowContent = $"{type.GetStatLabel()}: {character.CharacterStat} **|** Author: [__{character.CharacterAuthor}__]({character.GetAuthorLink()}) **|** [[__character link__]({character.GetCharacterLink()})]";
            if (searchQuery.CurrentRow == row)
            {
                rowTitle += " - ✅";
            }

            embed.AddField(rowTitle, rowContent);
        }

        embed.WithFooter($"Page {searchQuery.CurrentPage}/{searchQuery.Pages}");

        return embed.Build();
    }


    public static async Task<ISpawnedCharacter> SpawnCharacterAsync(ulong channelId, CommonCharacter commonCharacter)
    {
        var discordClient = DI.GetDiscordSocketClient;
        if (await discordClient.GetChannelAsync(channelId) is not ITextChannel channel)
        {
            throw new Exception($"Failed to get channel {channelId}");
        }

        await channel.EnsureExistInDbAsync();
        var webhook = await discordClient.CreateDiscordWebhookAsync(channel, commonCharacter);
        var newSpawnedCharacter = await DatabaseHelper.CreateSpawnedCharacterAsync(commonCharacter, webhook);

        return newSpawnedCharacter;
    }


    public static async Task<IWebhook> CreateDiscordWebhookAsync(this DiscordSocketClient discordClient, IIntegrationChannel channel, ICharacter character)
    {
        var characterName = character.CharacterName.Trim();
        var match = DISCORD_REGEX.Match(characterName);
        if (match.Success)
        {
            var discordCensored = match.Value.Replace('o', 'о').Replace('O', 'О');
            characterName = characterName.Replace(match.Value, discordCensored);
        }

        Stream? avatar = null;
        if (character.CharacterImageLink is not null)
        {
            try
            {
                avatar = await StaticStorage.CommonHttpClient.GetStreamAsync(character.CharacterImageLink);
            }
            catch (Exception e)
            {
                await discordClient.ReportErrorAsync(e);
            }
        }

        // TODO: thread safety
        // avatar ??= File.OpenRead(BotConfig.DEFAULT_AVATAR_FILE);

        var webhook = await channel.CreateWebhookAsync(characterName, avatar);
        return webhook;
    }


    public static async Task SendGreetingAsync(this ICharacter character, string username)
    {
        var characterMessage = character.CharacterFirstMessage
                                        .Replace("{{char}}", character.CharacterName)
                                        .Replace("{{user}}", $"**{username}**");

        var spawnedCharacter = (ISpawnedCharacter)character;
        var webhookClient = StaticStorage.CachedWebhookClients.GetById(spawnedCharacter.WebhookId)!;

        if (characterMessage.Length <= 2000)
        {
            await webhookClient.SendMessageAsync(characterMessage);
            return;
        }

        var chunkSize = characterMessage.Length > 3990 ? 1990 : characterMessage.Length / 2;
        var chunks = characterMessage.Chunk(chunkSize).Select(c => new string(c)).ToArray();

        var messageId = await webhookClient.SendMessageAsync(chunks[0]);

        var channel = (ITextChannel)await DI.GetDiscordSocketClient.GetChannelAsync(spawnedCharacter.DiscordChannelId);
        var message = await channel.GetMessageAsync(messageId);
        var thread = await channel.CreateThreadAsync("[MESSAGE LENGTH LIMIT EXCEEDED]", message: message);

        for (var i = 1; i < chunks.Length; i++)
        {
            await webhookClient.SendMessageAsync(chunks[i], threadId: thread.Id);
        }

        await thread.ModifyAsync(t => { t.Archived = t.Locked = true; });
    }


    #region CreateIntegration

    public static async Task CreateSakuraAiIntegrationAsync(SocketModal modal)
    {
        // Sending mail
        var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();
        var sakuraAiModule = (SakuraAiModule)IntegrationType.SakuraAI.GetIntegrationModule();

        SakuraSignInAttempt attempt;
        try
        {
            attempt = await sakuraAiModule.SendLoginEmailAsync(email);
        }
        catch (SakuraException e)
        {
            await modal.FollowupAsync(embed: $"{MessagesTemplates.WARN_SIGN_DISCORD} SakuraAI responded with error:\n```{e.Message}```".ToInlineEmbed(Color.Red));
            throw;
        }

        // Respond to user
        var msg = $"{IntegrationType.SakuraAI.GetIcon()} **SakuraAI**\n\n" +
                  $"Confirmation mail was sent to **{email}**. Please check your mailbox and follow further instructions.\n\n" +
                  $"- *It's recommended to log out of your SakuraAI account in the browser first, before you open a link in the mail; or simply open it in [incognito tab](https://support.google.com/chrome/answer/95464?hl=en&co=GENIE.Platform%3DDesktop&oco=1#:~:text=New%20Incognito%20Window).*\n" +
                  $"- *It may take up to a minute for the bot to react on succeful confirmation.*";

        await modal.FollowupAsync(embed: msg.ToInlineEmbed(bold: false, color: Color.Green));

        // Update db
        var data = StoredActionsHelper.CreateSakuraAiEnsureLoginData(attempt, (ulong)modal.ChannelId!, modal.User.Id);
        var newAction = new StoredAction(StoredActionType.SakuraAiEnsureLogin, data, maxAttemtps: 25);

        await using var db = DatabaseHelper.GetDbContext();
        await db.StoredActions.AddAsync(newAction);
        await db.SaveChangesAsync();
    }

    #endregion

}
