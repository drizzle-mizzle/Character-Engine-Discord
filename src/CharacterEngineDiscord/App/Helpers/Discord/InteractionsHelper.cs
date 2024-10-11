using System.Text.RegularExpressions;
using CharacterEngine.App.Helpers.Common;
using CharacterEngine.App.Helpers.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using RestSharp;

namespace CharacterEngine.App.Helpers.Discord;


public static class InteractionsHelper
{
    private static IServiceProvider _serviceProvider = null!;
    private static readonly ILogger _log = _serviceProvider.GetRequiredService<ILogger>();

    private static readonly Regex DISCORD_REGEX = new("discord", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);


    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }


    public const string SEP = "~sep~";


    public static SlashCommandProperties BuildStartCommand()
        => new SlashCommandBuilder().WithName("start").WithDescription("Register bot commands").WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild).Build();

    public static SlashCommandProperties BuildDisableCommand()
        => new SlashCommandBuilder().WithName("disable").WithDescription("Unregister all bot commands").WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild).Build();


    public static string NewCustomId(ModalActionType action, string data)
        => NewCustomId(Guid.NewGuid(), action, data);

    public static string NewCustomId(ModalData modalData)
        => NewCustomId(modalData.Id, modalData.ActionType, modalData.Data);

    public static string NewCustomId(Guid id, ModalActionType action, string data)
        => $"{id}{SEP}{action}{SEP}{data}";


    public static ModalData ParseCustomId(string customId)
    {
        var parts = customId.Split(SEP);
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
        for (var row = 1; row <= rows; row++)
        {
            var characterNumber = row + (searchQuery.CurrentPage - 1) * 10;
            var character = searchQuery.Characters.ElementAt(characterNumber - 1);

            var rowContent = $"{characterNumber}. {character.Name} [[link]({searchQuery.SelectedCharacter.OriginalLink})]";
            var rowFooter = $"{character.Stat} | Author: {character.Author}";
            if (searchQuery.CurrentRow == row)
            {
                rowContent += " - ✅";
            }

            embed.AddField(rowContent, rowFooter);
        }

        embed.WithFooter($"Page {searchQuery.CurrentPage}/{searchQuery.Pages}");

        return embed.Build();
    }


    public static async Task<(DiscordChannelSpawnedCharacter newChannelCharacter, ISpawnedCharacter newSpawnedCharacter)> SpawnCharacterAsync(
            ulong channelId, CommonCharacter character)
    {
        var discordClient = _serviceProvider.GetRequiredService<DiscordSocketClient>();
        var channel = await discordClient.GetChannelAsync(channelId) as ITextChannel;

        if (channel is null)
        {
            throw new Exception($"Failed to get channel {channelId}");
        }

        await channel.EnsureExistInDbAsync();
        var webhook = await discordClient.CreateDiscordWebhookAsync(channel, character);
        var newSpawnedCharacter = await DatabaseHelper.CreateSpawnedCharacterAsync(character, webhook);
        var newChannelCharacter = await newSpawnedCharacter.LinkToChannelAsync(channel);

        return (newChannelCharacter, newSpawnedCharacter);
    }


    public static async Task<IWebhook> CreateDiscordWebhookAsync(this DiscordSocketClient discordClient, IIntegrationChannel channel, CommonCharacter character)
    {
        var characterName = character.Name.Trim();
        var match = DISCORD_REGEX.Match(characterName);
        if (match.Success)
        {
            var discordCensored = match.Value.Replace('o', 'о').Replace('O', 'О');
            characterName = characterName.Replace(match.Value, discordCensored);
        }

        Stream? avatar = null;
        if (character.ImageLink is not null)
        {
            try
            {
                var request = new RestRequest(character.ImageLink);

                avatar = await RuntimeStorage.CommonRestClient.DownloadStreamAsync(request);
            }
            catch (Exception e)
            {
                await discordClient.ReportErrorAsync(e);
            }
        }

        avatar ??= File.OpenRead(BotConfig.DEFAULT_AVATAR_FILE);

        var webhook = await channel.CreateWebhookAsync(characterName, avatar);
        return webhook;
    }

}
