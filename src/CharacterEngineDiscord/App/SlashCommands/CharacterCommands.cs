using System.Text;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
    private const string NSFW_REQUIRED = "Sorry, but NSFW characters can be spawned only in age restricted channels. Please, mark channel as NSFW and try again.";

    public CharacterCommands(AppDbContext db)
    {
        _db = db;
    }


    [SlashCommand("spawn", "Spawn new character!")]
    public async Task SpawnCharacter(IntegrationType integrationType,
                                     [Summary(description: "Query to perform character search")] string? searchQuery = null,
                                     [Summary(description: "Needed for search only")] bool showNsfw = false,
                                     [Summary(description: "Not required; you can spawn character directly with its ID")] string? characterId = null,
                                     bool hide = false)
    {
        if (searchQuery is null && characterId is null)
        {
            throw new UserFriendlyException("search-query or character-id is required");
        }

        await RespondAsync(embed: MT.WAIT_MESSAGE, ephemeral: hide);

        var channel = (ITextChannel)Context.Channel;

        var guildIntegration = await DatabaseHelper.GetGuildIntegrationAsync(Context.Guild.Id, integrationType);
        if (guildIntegration is null)
        {
            throw new UserFriendlyException($"You have to setup a {integrationType.GetIcon()}{integrationType:G} intergration for this server first!");
        }

        var module = integrationType.GetIntegrationModule();
        if (string.IsNullOrWhiteSpace(characterId))
        {
            if (showNsfw && !channel.IsNsfw)
            {
                await ModifyOriginalResponseAsync(msg => { msg.Embed = NSFW_REQUIRED.ToInlineEmbed(Color.Purple); });

                return;
            }

            var characters = await module.SearchAsync(searchQuery!, showNsfw, guildIntegration);

            if (characters.Count == 0)
            {
                var message = $"{integrationType.GetIcon()} No characters were found by query **\"{searchQuery}\"**";
                await ModifyOriginalResponseAsync(msg => { msg.Embed = message.ToInlineEmbed(Color.Orange, false); });
                return;
            }

            var newSq = new SearchQuery(Context.Channel.Id, Context.User.Id, searchQuery!, characters, integrationType);
            MemoryStorage.SearchQueries.Add(newSq);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = MH.BuildSearchResultList(newSq);
                msg.Components = ButtonsHelper.BuildSearchButtons(newSq.Pages > 1);
            });

            return;
        }

        if (characterId.Contains('/'))
        {
            characterId = characterId.Split('/').Last();
        }

        var character = await module.GetCharacterAsync(characterId, guildIntegration);
        if (character.IsNfsw && !channel.IsNsfw)
        {
            await FollowupAsync(embed: NSFW_REQUIRED.ToInlineEmbed(Color.Purple));
            return;
        }

        var newSpawnedCharacter = await InteractionsHelper.SpawnCharacterAsync(Context.Channel.Id, character);
        var embed = await MH.BuildCharacterDescriptionCardAsync(newSpawnedCharacter, justSpawned: true);
        var modifyOriginalResponseAsync = ModifyOriginalResponseAsync(msg => { msg.Embed = embed; });

        await newSpawnedCharacter.SendGreetingAsync(((SocketGuildUser)Context.User).DisplayName);
        await modifyOriginalResponseAsync;
    }


    [SlashCommand("info", "Show character info card")]
    public async Task Info([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, bool hide = false)
    {
        await DeferAsync(ephemeral: hide);

        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var infoEmbed = await MH.BuildCharacterDescriptionCardAsync(spawnedCharacter, justSpawned: false);

        await FollowupAsync(embed: infoEmbed);
    }


    [SlashCommand("reset", "Start new chat")]
    public async Task ResetCharacter([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier)
    {
        await DeferAsync();

        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        spawnedCharacter.ResetWithNextMessage = true;

        var updateSpawnedCharacterAsync = DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);
        var message = $"{MT.OK_SIGN_DISCORD} Chat with **{spawnedCharacter.CharacterName}** reset successfully";

        var followupAsync = FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false));
        var greetingMessageId = await spawnedCharacter.SendGreetingAsync(((IGuildUser)Context.User).DisplayName);

        var cachedCharacter = MemoryStorage.CachedCharacters.Find(spawnedCharacter.Id)!;
        cachedCharacter.WideContextLastMessageId = greetingMessageId;

        await updateSpawnedCharacterAsync;
        await followupAsync;
    }


    [SlashCommand("remove", "Remove character")]
    public async Task RemoveCharacter([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier)
    {
        await DeferAsync();

        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);

        var deleteSpawnedCharacterAsync = DatabaseHelper.DeleteSpawnedCharacterAsync(spawnedCharacter);

        var webhookClient = MemoryStorage.CachedWebhookClients.Find(spawnedCharacter.WebhookId);
        if (webhookClient is not null)
        {
            try
            {
                await webhookClient.DeleteWebhookAsync();
            }
            catch
            {
                // care not
            }
        }

        var message = $"{MT.OK_SIGN_DISCORD} Character **{spawnedCharacter.CharacterName}** removed successfully";
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false));

        MemoryStorage.CachedCharacters.Remove(spawnedCharacter.Id);
        MemoryStorage.CachedWebhookClients.Remove(spawnedCharacter.WebhookId);
        await deleteSpawnedCharacterAsync;
    }


    #region configure

    [SlashCommand("messages-format", "Messages format")]
    public async Task MessagesFormat([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, MessagesFormatAction action, string? newFormat = null, bool hide = false)
    {
        await DeferAsync(ephemeral: hide);
        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var message = await InteractionsHelper.SharedMessagesFormatAsync(MessagesFormatTarget.character, action, spawnedCharacter, newFormat);

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    #region Toggle
    public enum TogglableSettings
    {
        [ChoiceDisplay("response-swipes")]
        ResponseSwipes,

        [ChoiceDisplay("quotes")]
        Quotes,

        [ChoiceDisplay("stop-button")]
        StopButton,
    }


    [SlashCommand("toggle", "Enable/disable feature")]
    public async Task Toggle([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, TogglableSettings feature)
    {
        await DeferAsync();

        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var characterName = spawnedCharacter.CharacterName;
        var featureName = feature.ToString("G").SplitWordsBySep(' ');

        bool newValue = default;
        switch (feature)
        {
            case TogglableSettings.ResponseSwipes:
            {
                newValue = spawnedCharacter.EnableSwipes ^= true;
                break;
            }
            case TogglableSettings.Quotes:
            {
                newValue = spawnedCharacter.EnableQuotes ^= true;
                break;
            }
            case TogglableSettings.StopButton:
            {
                newValue = spawnedCharacter.EnableStopButton ^= true;
                break;
            }
        }

        var message = $"{MT.OK_SIGN_DISCORD} **{featureName}** for character **{characterName}** was successfully changed to **{newValue}**";
        var updateSpawnedCharacterAsync = DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false));
        await updateSpawnedCharacterAsync;
    }


    #endregion


    #endregion



    #region Edits

    public enum EditableProp
    {
        [ChoiceDisplay("name")]
        Name,

        [ChoiceDisplay("avatar")]
        Avatar,

        [ChoiceDisplay("call-prefix")]
        CallPrefix,

        [ChoiceDisplay("chat-id")]
        ChatId,

        [ChoiceDisplay("wide-context-max-length")]
        FreewillMaxContextSize,

        [ChoiceDisplay("freewill-factor")]
        FreewillFactor,

        [ChoiceDisplay("first-message")]
        FirstMessage,

        [ChoiceDisplay("response-delay")]
        ResponseDelay,
    }


    [SlashCommand("edit", "Update character's info, call prefix, etc")]
    public async Task Edit([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, EditableProp property, string newValue)
    {
        await DeferAsync();

        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var characterName = spawnedCharacter.CharacterName;

        string? oldValue = null;
        switch (property)
        {
            case EditableProp.CallPrefix:
            {
                oldValue = spawnedCharacter.CallPrefix;
                spawnedCharacter.CallPrefix = newValue;

                var cachedCharacter = MemoryStorage.CachedCharacters.Find(spawnedCharacter.Id)!;
                cachedCharacter.CallPrefix = newValue;

                break;
            }
            case EditableProp.Name:
            {
                oldValue = spawnedCharacter.CharacterName;
                spawnedCharacter.CharacterName = newValue;

                await UpdateNameAsync(spawnedCharacter);
                break;
            }
            case EditableProp.Avatar:
            {
                oldValue = spawnedCharacter.CharacterImageLink;
                spawnedCharacter.CharacterImageLink = newValue;

                await UpdateAvatarAsync(spawnedCharacter, newValue);
                break;
            }
            case EditableProp.ChatId:
            {
                UpdateChatId(spawnedCharacter, newValue);
                break;
            }
            case EditableProp.FreewillMaxContextSize:
            {
                spawnedCharacter.FreewillContextSize = uint.Parse(newValue);
                break;
            }
            case EditableProp.FreewillFactor:
            {
                oldValue = spawnedCharacter.FreewillFactor.ToString();

                var newFreewillFactor = double.Parse(newValue);
                if (newFreewillFactor is < 0 or > 100)
                {
                    throw new UserFriendlyException("Allowed values: 0.00-100.00");
                }

                spawnedCharacter.FreewillFactor = newFreewillFactor;

                var cachedCharacter = MemoryStorage.CachedCharacters.Find(spawnedCharacter.Id)!;
                cachedCharacter.FreewillFactor = newFreewillFactor;

                break;
            }
            case EditableProp.FirstMessage:
            {
                oldValue = spawnedCharacter.CharacterFirstMessage;
                spawnedCharacter.CharacterFirstMessage = newValue;

                break;
            }
            case EditableProp.ResponseDelay:
            {
                oldValue = spawnedCharacter.ResponseDelay.ToString();
                spawnedCharacter.ResponseDelay = uint.Parse(newValue);

                break;
            }
        }

        await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

        var message = new StringBuilder();
        var propertyName = property.ToString("G").SplitWordsBySep(' ');
        message.Append($"{MT.OK_SIGN_DISCORD} **{propertyName}** for character **{characterName}** was successfully changed ");

        if (oldValue is not null)
        {
            message.Append($"from **{oldValue}** ");
        }

        message.Append($"to **{newValue}**");

        await FollowupAsync(embed: message.ToString().ToInlineEmbed(Color.Green, bold: false));
    }


    private async Task UpdateNameAsync(ISpawnedCharacter spawnedCharacter)
    {
        var webhookClient = MemoryStorage.CachedWebhookClients.Find(spawnedCharacter.WebhookId);
        if (webhookClient is null)
        {
            await InteractionsHelper.CreateDiscordWebhookAsync((IIntegrationChannel)Context.Channel, spawnedCharacter);
        }
        else
        {
            await webhookClient.ModifyWebhookAsync(w => { w.Name = spawnedCharacter.CharacterName; });
        }
    }

    private async Task UpdateAvatarAsync(ISpawnedCharacter spawnedCharacter, string newAvatarUrl)
    {
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


    private static void UpdateChatId(ISpawnedCharacter spawnedCharacter, string newChatId)
    {
        switch (spawnedCharacter)
        {
            case SakuraAiSpawnedCharacter sakuraAiSpawnedCharacter:
            {
                sakuraAiSpawnedCharacter.SakuraChatId = newChatId;
                break;
            }
            case CaiSpawnedCharacter caiSpawnedCharacter:
            {
                caiSpawnedCharacter.CaiChatId = newChatId;
                break;
            }
        }
    }

    #endregion


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
