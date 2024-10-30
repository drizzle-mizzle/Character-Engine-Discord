using System.ComponentModel;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;
using MT = CharacterEngine.App.Helpers.Discord.MessagesTemplates;

namespace CharacterEngine.App.SlashCommands;


[ValidateAccessLevel(AccessLevels.Manager)]
[ValidateChannelPermissions]
[Group("character", "Basic characters commands")]
public class CharacterCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private const string ANY_IDENTIFIER_DESC = "Character call prefix or User ID (Webhook ID)";


    public CharacterCommands(AppDbContext db)
    {
        _db = db;
    }


    [SlashCommand("spawn", "Spawn new character!")]
    public async Task SpawnCharacter(string query, IntegrationType integrationType)
    {
        await RespondAsync(embed: MT.WAIT_MESSAGE);

        var module = integrationType.GetIntegrationModule();
        var characters = await module.SearchAsync(query);

        if (characters.Count == 0)
        {
            var message = $"{integrationType.GetIcon()} No characters were found by query **\"{query}\"**";
            await ModifyOriginalResponseAsync(msg => { msg.Embed = message.ToInlineEmbed(Color.Orange, false); });
            return;
        }

        var searchQuery = new SearchQuery(Context.Channel.Id, Context.User.Id, query, characters, integrationType);
        MemoryStorage.SearchQueries.Add(searchQuery);

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = InteractionsHelper.BuildSearchResultList(searchQuery);
            msg.Components = ButtonsHelper.BuildSearchButtons(searchQuery.Pages > 1);
        });
    }


    [SlashCommand("reset", "Start new chat")]
    public async Task ResetCharacter([Description(ANY_IDENTIFIER_DESC)] string anyIdentifier)
    {
        await DeferAsync();

        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        spawnedCharacter.ResetWithNextMessage = true;

        var tasks = new List<Task>();
        tasks.Add(DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter));

        var message = $"{MT.OK_SIGN_DISCORD} Chat with **{spawnedCharacter.CharacterName}** reset successfully";
        tasks.Add(FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false)));

        var user = (IGuildUser)Context.User;
        tasks.Add(spawnedCharacter.SendGreetingAsync(user.DisplayName));

        Task.WaitAll(tasks.ToArray());
    }


    [SlashCommand("remove", "Remove character")]
    public async Task RemoveCharacter([Description(ANY_IDENTIFIER_DESC)] string anyIdentifier)
    {
        await DeferAsync();

        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);

        var tasks = new List<Task>();
        tasks.Add(DatabaseHelper.DeleteSpawnedCharacterAsync(spawnedCharacter));

        var webhookClient = MemoryStorage.CachedWebhookClients.Find(spawnedCharacter.WebhookId);
        if (webhookClient is not null)
        {
            tasks.Add(webhookClient.DeleteWebhookAsync());
        }

        var message = $"{MT.OK_SIGN_DISCORD} Character **{spawnedCharacter.CharacterName}** removed successfully";
        tasks.Add(FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false)));

        MemoryStorage.CachedCharacters.Remove(spawnedCharacter.Id);
        MemoryStorage.CachedWebhookClients.Remove(spawnedCharacter.WebhookId);
    }



    public enum ChangeAction
    {
        CallPrefix, CharacterName,

        [Description("Image URL")]
        Avatar
    }


    [SlashCommand("update", "Update character's info, call prefix, etc")]
    public async Task Update([Description(ANY_IDENTIFIER_DESC)] string anyIdentifier, ChangeAction propertyToUpdate, string newValue)
    {
        await DeferAsync();

        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var characterName = spawnedCharacter.CharacterName;
        var propertyName = propertyToUpdate.ToString("G").SplitWordsBySep(' ');

        switch (propertyToUpdate)
        {
            case ChangeAction.CallPrefix:
            {
                UpdateCallPrefix(spawnedCharacter, newValue);
                break;
            }
            case ChangeAction.CharacterName:
            {
                await UpdateNameAsync(spawnedCharacter, newValue);
                break;
            }
            case ChangeAction.Avatar:
            {
                await UpdateAvatarAsync(spawnedCharacter, newValue);
                break;
            }
        }

        var message = $"{MT.OK_SIGN_DISCORD} **{propertyName}** for character **{characterName}** was successfully changed to **{newValue}**";
        var updateSpawnedCharacterAsync = DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false));
        await updateSpawnedCharacterAsync;
    }


    public enum MessagesFormatAction { Show, Update, ResetDefault }

    [SlashCommand("messages-format", "Messages format")]
    public async Task MessagesFormat([Description(ANY_IDENTIFIER_DESC)] string anyIdentifier, MessagesFormatAction action, string? newFormat = null)
    {
        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);

        var message = string.Empty;
        switch (action)
        {
            case MessagesFormatAction.Show:
            {
                var inherit = string.Empty;
                var format = spawnedCharacter.MessagesFormat;
                if (format is null)
                {
                    var channel = await _db.DiscordChannels.Include(c => c.DiscordGuild).FirstAsync(c => c.Id == Context.Channel.Id);
                    if (channel.MessagesFormat is not null)
                    {
                        format = channel.MessagesFormat;
                        inherit = " (inherited from channel-wide messages format setting)";
                    }
                    else
                    {
                        format = channel.DiscordGuild?.MessagesFormat ?? BotConfig.DEFAULT_MESSAGES_FORMAT;
                        inherit = " (inherited from guild-wide messages format setting)";
                    }
                }

                var preview = MH.BuildMessageFormatPreview(format);
                message = $"Current messages format for character **{spawnedCharacter.CharacterName}**{inherit}:\n" +
                          $"```{format.Replace("\\n", "\\n\n")}```\n" +
                          $"**Preview:**\n{preview}";

                break;
            }
            case MessagesFormatAction.Update:
            {
                if (newFormat is null)
                {
                    throw new UserFriendlyException("Specify a new messages format");
                }

                if (!newFormat.Contains(MH.MF_MSG))
                {
                    throw new UserFriendlyException($"Add {MH.MF_MSG} placeholder");
                }

                if (newFormat.Contains(MH.MF_REF_MSG))
                {
                    var iBegin = newFormat.IndexOf(MH.MF_REF_BEGIN, StringComparison.Ordinal);
                    var iEnd = newFormat.IndexOf(MH.MF_REF_END, StringComparison.Ordinal);
                    var iMsg = newFormat.IndexOf(MH.MF_REF_MSG, StringComparison.Ordinal);

                    if (iBegin == -1 || iEnd == -1 || iBegin > iMsg || iEnd < iMsg)
                    {
                        throw new UserFriendlyException($"{MH.MF_REF_MSG} placeholder can work only with {MH.MF_REF_BEGIN} and {MH.MF_REF_END} placeholders around it: `{MH.MF_REF_BEGIN} {MH.MF_REF_MSG} {MH.MF_REF_END}`");
                    }
                }

                spawnedCharacter.MessagesFormat = newFormat;
                await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

                var preview = MH.BuildMessageFormatPreview(newFormat);
                message = $"{MT.OK_SIGN_DISCORD} Messages format was changed successfully.\n" +
                          $"**Preview:**\n{preview}";

                break;
            }
            case MessagesFormatAction.ResetDefault:
            {
                spawnedCharacter.MessagesFormat = BotConfig.DEFAULT_MESSAGES_FORMAT;
                await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

                var preview = MH.BuildMessageFormatPreview(BotConfig.DEFAULT_MESSAGES_FORMAT);
                message = $"{MT.OK_SIGN_DISCORD} Messages format was reset to default value successfully.\n" +
                          $"**Preview:**\n{preview}";

                break;
            }
        }

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }

    private static void UpdateCallPrefix(ISpawnedCharacter spawnedCharacter, string newCallPrefix)
    {
        spawnedCharacter.CallPrefix = newCallPrefix;
        MemoryStorage.CachedCharacters.Remove(spawnedCharacter.Id);
        MemoryStorage.CachedCharacters.Add(spawnedCharacter);
    }

    private async Task UpdateNameAsync(ISpawnedCharacter spawnedCharacter, string newName)
    {
        spawnedCharacter.CharacterName = newName;

        var webhookClient = MemoryStorage.CachedWebhookClients.Find(spawnedCharacter.WebhookId);
        if (webhookClient is null)
        {
            await InteractionsHelper.CreateDiscordWebhookAsync((IIntegrationChannel)Context.Channel, spawnedCharacter);
        }
        else
        {
            await webhookClient.ModifyWebhookAsync(w => { w.Name = newName; });
        }
    }

    private async Task UpdateAvatarAsync(ISpawnedCharacter spawnedCharacter, string newAvatarUrl)
    {
        spawnedCharacter.CharacterImageLink = newAvatarUrl;

        var webhookClient = MemoryStorage.CachedWebhookClients.Find(spawnedCharacter.WebhookId);
        if (webhookClient is null)
        {
            await InteractionsHelper.CreateDiscordWebhookAsync((IIntegrationChannel)Context.Channel, spawnedCharacter);
        }
        else
        {
            var image = await CommonHelper.DownloadFileAsync(newAvatarUrl);
            if (image is null)
            {
                throw new UserFriendlyException("Failed to download image");
            }

            await webhookClient.ModifyWebhookAsync(w => { w.Image = new Image(image); });
        }
    }



    private static async Task<ISpawnedCharacter> FindCharacterAsync(string anyIdentifier, ulong channelId)
    {
        var cachedCharacter = MemoryStorage.CachedCharacters.Find(anyIdentifier, channelId);
        if (cachedCharacter is null)
        {
            throw new UserFriendlyException("Character not found");
        }

        var spawnedCharacter = await DatabaseHelper.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
        if (spawnedCharacter is null)
        {
            throw new UserFriendlyException("Character not found");
        }

        return spawnedCharacter;
    }
}
