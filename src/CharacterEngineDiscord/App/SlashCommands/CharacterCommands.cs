using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using Discord.Interactions;
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

        var guildIntegration = await DatabaseHelper.GetGuildIntegrationAsync(Context.Guild.Id, integrationType);
        if (guildIntegration is null)
        {
            throw new UserFriendlyException($"You have to setup the {integrationType:G} intergration for this server first!");
        }

        var module = integrationType.GetIntegrationModule();
        var characters = await module.SearchAsync(query, guildIntegration);

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
            msg.Embed = MH.BuildSearchResultList(searchQuery);
            msg.Components = ButtonsHelper.BuildSearchButtons(searchQuery.Pages > 1);
        });
    }


    [SlashCommand("info", "Show character info card")]
    public async Task Info([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier)
    {
        await DeferAsync();

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

        var tasks = new List<Task>();
        tasks.Add(DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter));

        var message = $"{MT.OK_SIGN_DISCORD} Chat with **{spawnedCharacter.CharacterName}** reset successfully";
        tasks.Add(FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false)));

        var user = (IGuildUser)Context.User;
        tasks.Add(spawnedCharacter.SendGreetingAsync(user.DisplayName));

        Task.WaitAll(tasks.ToArray());
    }


    [SlashCommand("remove", "Remove character")]
    public async Task RemoveCharacter([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier)
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

        Task.WaitAll(tasks.ToArray());
    }


    #region configure

    [SlashCommand("messages-format", "Messages format")]
    public async Task MessagesFormat([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, MessagesFormatAction action, string? newFormat = null)
    {
        await DeferAsync();
        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var message = await InteractionsHelper.SharedMessagesFormatAsync(MessagesFormatTarget.character, action, spawnedCharacter, newFormat);

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    [SlashCommand("freewill-factor", "Chance (0.0-100.0) that character will randomly respond to some user's message; 0 - disable feature")]
    public async Task FreewillFactor([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, [MinValue(0)] [MaxValue(100)] double factor)
    {
        await DeferAsync();
        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);

        var message = $"{MT.OK_SIGN_DISCORD} Freewill factor was successfully changed from **{spawnedCharacter.FreewillFactor}** to **{factor}**";

        spawnedCharacter.FreewillFactor = factor;
        var updateSpawnedCharacterAsync = DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

        MemoryStorage.CachedCharacters.Remove(spawnedCharacter.Id);
        MemoryStorage.CachedCharacters.Add(spawnedCharacter);

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
        await updateSpawnedCharacterAsync;
    }


    #region Toggle

    private const string TOGGLABLE_SETTINGS_DESC = "ResponseSwipes - Back and forth arrow buttons that allows to choose characters messages\n" +
                                                   "WideContext - EXPERIMENTAL!\n" +
                                                   "Quotes - Character quotes the message it responds to\n" +
                                                   "StopButton - You'll need it for character-vs-characters conversations";
    public enum TogglableSettings
    {
        [ChoiceDisplay("response-swipes")]
        ResponseSwipes,

        [ChoiceDisplay("wide-context")]
        WideContext,

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
            case TogglableSettings.WideContext:
            {
                newValue = spawnedCharacter.EnableWideContext ^= true;
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
    }


    [SlashCommand("edit", "Update character's info, call prefix, etc")]
    public async Task Edit([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, EditableProp dataToEdit, string newValue)
    {
        await DeferAsync();

        var spawnedCharacter = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var characterName = spawnedCharacter.CharacterName;
        var propertyName = dataToEdit.ToString("G").SplitWordsBySep(' ');

        switch (dataToEdit)
        {
            case EditableProp.CallPrefix:
            {
                UpdateCallPrefix(spawnedCharacter, newValue);
                break;
            }
            case EditableProp.Name:
            {
                await UpdateNameAsync(spawnedCharacter, newValue);
                break;
            }
            case EditableProp.Avatar:
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
