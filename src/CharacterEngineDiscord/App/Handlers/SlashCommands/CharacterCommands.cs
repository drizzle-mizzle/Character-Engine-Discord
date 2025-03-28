using System.Text;
using CharacterAi.Client.Exceptions;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Decorators;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Masters;
using CharacterEngine.App.Repositories;
using CharacterEngine.App.Repositories.Storages;
using CharacterEngine.App.Services;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Modules.Abstractions;
using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Abstractions.Characters;
using CharacterEngineDiscord.Shared.Helpers;
using CharacterEngineDiscord.Shared.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using static CharacterEngine.App.Helpers.ValidationsHelper;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;
using MT = CharacterEngine.App.Helpers.Discord.MessagesTemplates;


namespace CharacterEngine.App.Handlers.SlashCommands;


[ValidateChannelPermissions]
[Group("character", "Basic characters commands")]
public class CharacterCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly CharactersDbRepository _charactersDbRepository;
    private readonly IntegrationsDbRepository _integrationsDbRepository;
    private readonly CacheRepository _cacheRepository;
    private readonly InteractionsMaster _interactionsMaster;
    private readonly IntegrationsMaster _integrationsMaster;
    private const string ANY_IDENTIFIER_DESC = "Character call prefix or User ID (Webhook ID)";
    private const string NSFW_REQUIRED = "Sorry, but NSFW characters can be spawned only in age restricted channels. Please, mark channel as NSFW and try again.";


    public CharacterCommands(
        AppDbContext db,
        CharactersDbRepository charactersDbRepository,
        IntegrationsDbRepository integrationsDbRepository,
        CacheRepository cacheRepository,
        InteractionsMaster interactionsMaster,
        IntegrationsMaster integrationsMaster
    )
    {
        _db = db;
        _charactersDbRepository = charactersDbRepository;
        _integrationsDbRepository = integrationsDbRepository;
        _cacheRepository = cacheRepository;
        _interactionsMaster = interactionsMaster;
        _integrationsMaster = integrationsMaster;
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("spawn", "Spawn new character!")]
    public async Task SpawnCharacter([Summary(description: "A platform to base character on")] IntegrationType integrationType,
                                     [Summary(description: "A query to perform character search")] string? searchQuery = null,
                                     [Summary(description: "Can be used instead of search to spawn a character with its ID")] string? characterId = null,
                                     [Summary(description: "optional; show nsfw characters in search")] bool showNsfw = false,
                                     [Summary(description: "optional; characters source to use, required for certain integration types")] CharacterSourceType? useCatalog = null,
                                     bool hide = false)
    {
        var channel = (ITextChannel)Context.Channel;
        if (showNsfw && !channel.IsNsfw)
        {
            throw new UserFriendlyException(NSFW_REQUIRED, bold: true, Color.Purple);
        }

        if (searchQuery is null && characterId is null)
        {
            throw new UserFriendlyException("search-query or character-id parameter is required");
        }

        await RespondAsync(embed: MT.WAIT_MESSAGE, ephemeral: hide);
        var originalResponse = await GetOriginalResponseAsync();


        bool isThread;
        IReadOnlyCollection<IWebhook>? webhooks;
        if (channel is SocketThreadChannel { ParentChannel: ITextChannel parentChannel })
        {
            webhooks = await parentChannel.GetWebhooksAsync();
            isThread = true;
        }
        else
        {
            webhooks = await channel.GetWebhooksAsync();
            isThread = false;
        }

        if (webhooks.Count == 15)
        {
            throw new UserFriendlyException("This channel already has 15 webhooks, which is the Discord limit. To create a new character, you will need to remove an existing one from this channel; this can be done with `/character remove` command.");
        }

        var guildIntegration = await _integrationsDbRepository.GetGuildIntegrationAsync(Context.Guild.Id, integrationType);
        if (guildIntegration is null)
        {
            throw new UserFriendlyException($"You have to setup **{integrationType:G}** intergration for this server first!", bold: false);
        }

        ISearchModule searchModule;
        if (!guildIntegration.IsChatOnly)
        {
            searchModule = IntegrationsHub.GetSearchModule(integrationType);
        }
        else if (useCatalog is CharacterSourceType sourceType)
        {
            searchModule = IntegrationsHub.GetSearchModule(sourceType);
        }
        else
        {
            throw new UserFriendlyException($"{MT.WARN_SIGN_DISCORD} You have to specify the **use-catalog** parameter", bold: false);
        }

        if (string.IsNullOrWhiteSpace(characterId))
        {
            await SearchCharacterAsync();
        }
        else
        {
            try
            {
                await SpawnCharacterByIdAsync();
            }
            catch (CharacterAiException)
            {
                throw new UserFriendlyException($"{integrationType.GetIcon()} Failed to find character with id \"{characterId.Trim()}\"");
            }
        }

        return;

        async Task SearchCharacterAsync()
        {
            var characters = await searchModule.SearchAsync(searchQuery!, showNsfw, guildIntegration);
            if (characters.Count == 0)
            {
                throw new UserFriendlyException($"{integrationType.GetIcon()} No characters were found by search query **\"{searchQuery}\"** [ show-nsfw: **{showNsfw}** ]", bold: false);
            }

            var newSq = new SearchQuery(originalResponse.Id, Context.User.Id, searchQuery!, characters, integrationType);
            _cacheRepository.ActiveSearchQueries.Add(newSq);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = MH.BuildSearchResultList(newSq);
                msg.Components = ButtonsHelper.BuildSearchButtons(newSq.Pages > 1);
            });
        }

        async Task SpawnCharacterByIdAsync()
        {
            var characterAdapter = await searchModule.GetCharacterInfoAsync(characterId.Trim(), guildIntegration);
            var commonCharacter = characterAdapter.ToCommonCharacter();

            if (commonCharacter.IsNfsw && !channel.IsNsfw)
            {
                throw new UserFriendlyException(NSFW_REQUIRED, bold: true, Color.Purple);
            }

            var newSpawnedCharacter = await _integrationsMaster.SpawnCharacterAsync(Context.Channel.Id, characterAdapter.ToCommonCharacter(), guildIntegration);

            var embed = MH.BuildCharacterDescriptionCard(newSpawnedCharacter, justSpawned: true);
            var modifyOriginalResponseAsync = ModifyOriginalResponseAsync(msg => { msg.Embed = embed; });

            var webhook = _cacheRepository.CachedWebhookClients.FindOrCreate(newSpawnedCharacter.WebhookId, newSpawnedCharacter.WebhookToken);
            var activeCharacter = new ActiveCharacterDecorator(newSpawnedCharacter, webhook);

            await activeCharacter.SendGreetingAsync(Context.User.Mention, threadId: isThread ? channel.Id : null);
            await modifyOriginalResponseAsync;
        }
    }


    [SlashCommand("info", "Show character info card")]
    public async Task Info([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, bool hide = false)
    {
        await DeferAsync(ephemeral: hide);

        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var infoEmbed = MH.BuildCharacterDescriptionCard(scc.spawnedCharacter, justSpawned: false);

        await FollowupAsync(embed: infoEmbed);
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("reset", "Start new chat")]
    public async Task ResetCharacter([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier)
    {
        await DeferAsync();

        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var spawnedCharacter = scc.spawnedCharacter;

        switch (spawnedCharacter)
        {
            case IAdoptedCharacter:
            {
                var history = _db.ChatHistories.Where(message => message.SpawnedCharacterId == spawnedCharacter.Id);
                _db.ChatHistories.RemoveRange(history);
                break;
            }
            case CaiSpawnedCharacter caiSpawnedCharacter:
            {
                caiSpawnedCharacter.CaiChatId = null;
                break;
            }
            case SakuraAiSpawnedCharacter sakuraAiSpawnedCharacter:
            {
                sakuraAiSpawnedCharacter.SakuraChatId = null;
                break;
            }
        }

        var updateSpawnedCharacterAsync = _charactersDbRepository.UpdateSpawnedCharacterAsync(spawnedCharacter);
        var message = $"{MT.OK_SIGN_DISCORD} Chat with {spawnedCharacter.GetMention()} reset successfully";

        var followupAsync = FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false));

        var webhook = _cacheRepository.CachedWebhookClients.FindOrCreate(spawnedCharacter.WebhookId, spawnedCharacter.WebhookToken);
        var activeCharacter = new ActiveCharacterDecorator(spawnedCharacter, webhook);
        var greetingMessageId = await activeCharacter.SendGreetingAsync(Context.User.Mention);

        var cachedCharacter = _cacheRepository.CachedCharacters.Find(spawnedCharacter.Id)!;
        cachedCharacter.WideContextLastMessageId = greetingMessageId;

        await updateSpawnedCharacterAsync;
        await followupAsync;
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("remove", "Remove character")]
    public async Task RemoveCharacter([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier)
    {
        await DeferAsync();

        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var spawnedCharacter = scc.spawnedCharacter;

        var deleteSpawnedCharacterAsync = _charactersDbRepository.DeleteSpawnedCharacterAsync(spawnedCharacter.Id);

        var webhookClient = CachedWebhookClientsStorage.Find(spawnedCharacter.WebhookId);
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

        await deleteSpawnedCharacterAsync;

        var message = $"{MT.OK_SIGN_DISCORD} Character {spawnedCharacter.GetMention()} removed successfully";
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false));

        _cacheRepository.CachedCharacters.Remove(spawnedCharacter.Id);
        _cacheRepository.CachedWebhookClients.Remove(spawnedCharacter.WebhookId);
    }


    [SlashCommand("hunted-users", "Make character hunt certain user or another character")]
    public async Task HuntedUsers(UserAction action, [Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, IGuildUser? user = null, string? userIdOrCharacterCallPrefix = null)
    {
        if (action is not UserAction.show)
        {
            await ValidateAccessLevelAsync(AccessLevel.Manager, (SocketGuildUser)Context.User);
        }

        await DeferAsync();

        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var spawnedCharacter = scc.spawnedCharacter;
        var cahcedCharacter = scc.cachedCharacter;

        var huntedUsers = await _db.HuntedUsers.Where(hu => hu.SpawnedCharacterId == spawnedCharacter.Id).ToListAsync();

        switch (action)
        {
            case UserAction.show:
            {
                var list = new StringBuilder();

                foreach (var huntedUser in huntedUsers)
                {
                    var huntedGuilduser = await Context.Guild.GetUserAsync(huntedUser.DiscordUserId);

                    list.AppendLine($"**{huntedGuilduser.Username}**");
                }

                var embed = new EmbedBuilder().WithColor(Color.LighterGrey)
                                              .WithTitle($"Hunted users of {spawnedCharacter.GetMention()} ({huntedUsers.Count})")
                                              .WithDescription(list.ToString());

                await FollowupAsync(embed: embed.Build());
                return;
            }
            case UserAction.clearAll:
            {
                _db.HuntedUsers.RemoveRange(huntedUsers);
                await _db.SaveChangesAsync();

                cahcedCharacter.HuntedUsers.Clear();

                await FollowupAsync(embed: $"Hunted users list for {spawnedCharacter.GetMention()} has been cleared".ToInlineEmbed(Color.Green, bold: true));
                return;
            }
        }

        if (user is null && userIdOrCharacterCallPrefix is null)
        {
            throw new UserFriendlyException($"Specify the user to {action:G}");
        }

        ulong huntedUserId;
        if (user is null)
        {
            var otherCharacter = _cacheRepository.CachedCharacters.Find(userIdOrCharacterCallPrefix!, Context.Channel.Id);
            huntedUserId = ulong.Parse(otherCharacter?.WebhookId ?? userIdOrCharacterCallPrefix!);
        }
        else
        {
            huntedUserId = user.Id;
        }

        var guildUser = user ?? await Context.Guild.GetUserAsync(huntedUserId);
        var mention = guildUser?.Mention ?? $"User <@{huntedUserId}>";

        string message = null!;

        switch (action)
        {
            case UserAction.add when huntedUsers.Any(hu => hu.DiscordUserId == huntedUserId):
            {
                throw new UserFriendlyException($"{mention} is already hunted by {spawnedCharacter.GetMention()}");
            }
            case UserAction.add:
            {
                var newHuntedUser = new HuntedUser
                {
                    DiscordUserId = huntedUserId,
                    SpawnedCharacterId = spawnedCharacter.Id
                };

                _db.HuntedUsers.Add(newHuntedUser);
                cahcedCharacter.HuntedUsers.Add(huntedUserId);

                message = $":ghost: {mention} now hunted by {spawnedCharacter.GetMention()}";

                break;
            }
            case UserAction.remove:
            {
                var huntedUser = huntedUsers.FirstOrDefault(hu => hu.DiscordUserId == huntedUserId);

                if (huntedUser is null)
                {
                    throw new UserFriendlyException($"{mention} is not a not hunted by {spawnedCharacter.GetMention()}");
                }

                _db.HuntedUsers.Remove(huntedUser);
                cahcedCharacter.HuntedUsers.Remove(huntedUserId);

                message = $"{mention} is not hunted by {spawnedCharacter.GetMention()} anymore :ghost:";
                break;
            }
        }

        await _db.SaveChangesAsync();
        await FollowupAsync(embed: message.ToInlineEmbed(Color.LighterGrey, bold: true, imageUrl: guildUser?.GetAvatarUrl(), imageAsThumb: false));
    }


    #region configure

    [SlashCommand("messages-format", "Change character messages format")]
    public async Task MessagesFormat(SinglePropertyAction action, [Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, string? newFormat = null, bool hide = false)
    {
        if (action is not SinglePropertyAction.show)
        {
            await ValidateAccessLevelAsync(AccessLevel.Manager, (SocketGuildUser)Context.User);
        }

        await DeferAsync(ephemeral: hide);
        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);

        string message = null!;

        switch (action)
        {
            case SinglePropertyAction.show:
            {
                message = $"Messages format for character {scc.spawnedCharacter.GetMention()}\n" + await _interactionsMaster.BuildCharacterMessagesFormatDisplayAsync(scc.spawnedCharacter);
                break;
            }
            case SinglePropertyAction.update:
            {
                if (newFormat is null)
                {
                    throw new UserFriendlyException("Specify the new-format parameter");
                }

                message = await UpdateCharacterMessagesFormatAsync(scc.spawnedCharacter, newFormat);
                break;
            }
            case SinglePropertyAction.resetDefault:
            {
                message = await UpdateCharacterMessagesFormatAsync(scc.spawnedCharacter, null);
                break;
            }
        }

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    [SlashCommand("system-prompt", "Change character system prompt")]
    public async Task SystemPrompt(SinglePropertyAction action, [Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, string? newPrompt = null, bool hide = false)
    {
        if (action is not SinglePropertyAction.show)
        {
            await ValidateAccessLevelAsync(AccessLevel.Manager, (SocketGuildUser)Context.User);
        }

        await DeferAsync();
        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);

        string message = null!;

        switch (action)
        {
            case SinglePropertyAction.show:
            {
                message = $"System prompt for character {scc.spawnedCharacter.GetMention()}\n" + await _interactionsMaster.BuildCharacterSystemPromptDisplayAsync(scc.spawnedCharacter);
                break;
            }
            case SinglePropertyAction.update:
            {
                if (newPrompt is null)
                {
                    throw new UserFriendlyException("Specify the new-prompt parameter");
                }

                message = await UpdateCharacterSystemPromptAsync(scc.spawnedCharacter, newPrompt);
                break;
            }
            case SinglePropertyAction.resetDefault:
            {
                message = await UpdateCharacterSystemPromptAsync(scc.spawnedCharacter, null);
                break;
            }
        }

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("openrouter-settings", "Display character OpenRouter settings")]
    public async Task OpenRouterSettings([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier)
    {
        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);

        var jsonCharacter = JsonConvert.SerializeObject(scc.spawnedCharacter, Formatting.Indented);
        var settings = JsonConvert.DeserializeObject<OpenRouterSettings>(jsonCharacter);
        var jsonSettings = JsonConvert.SerializeObject(settings, Formatting.Indented);

        var customId = InteractionsHelper.NewCustomId(ModalActionType.OpenRouterSettings, $"{scc.spawnedCharacter.Id}~{SettingTarget.Character:D}");
        var characterName = scc.spawnedCharacter.CharacterName.Length <= 18 ? scc.spawnedCharacter.CharacterName : scc.spawnedCharacter.CharacterName[..18];

        var modal = new ModalBuilder().WithTitle($"Edit {characterName}'s OpenRouter settings")
                                      .WithCustomId(customId)
                                      .AddTextInput("Settings:", "settings", TextInputStyle.Paragraph, value: jsonSettings)
                                      .Build();

        await RespondWithModalAsync(modal); // next in EnsureSakuraAiLoginAsync()
    }

    #endregion


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


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("toggle", "Enable/disable feature")]
    public async Task Toggle([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, TogglableSettings feature)
    {
        await DeferAsync();

        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var spawnedCharacter = scc.spawnedCharacter;

        var featureName = feature.ToString("G").SplitWordsBySep(' ');

        bool newValue = false;
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

        var message = $"{MT.OK_SIGN_DISCORD} **{featureName}** for character {spawnedCharacter.GetMention()} was successfully changed to **{newValue}**";
        var updateSpawnedCharacterAsync = _charactersDbRepository.UpdateSpawnedCharacterAsync(spawnedCharacter);

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false));
        await updateSpawnedCharacterAsync;
    }


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


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("edit", "Update character's info, call prefix, etc")]
    public async Task Edit([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, EditableProp property, string newValue)
    {
        await DeferAsync();

        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var spawnedCharacter = scc.spawnedCharacter;
        var cachedCharacter = scc.cachedCharacter;

        string? oldValue = null;
        switch (property)
        {
            case EditableProp.CallPrefix:
            {
                oldValue = spawnedCharacter.CallPrefix;
                spawnedCharacter.CallPrefix = newValue;
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

        await _charactersDbRepository.UpdateSpawnedCharacterAsync(spawnedCharacter);

        var message = new StringBuilder();
        var propertyName = property.ToString("G").SplitWordsBySep(' ').ToLower().CapitalizeFirst();
        message.Append($"{MT.OK_SIGN_DISCORD} **{propertyName}** for character {spawnedCharacter.GetMention()} was successfully changed ");

        if (oldValue is not null)
        {
            message.Append($"from **{oldValue}** ");
        }

        message.Append($"to **{newValue}**");

        await FollowupAsync(embed: message.ToString().ToInlineEmbed(Color.Green, bold: false));
    }


    private async Task UpdateNameAsync(ISpawnedCharacter spawnedCharacter)
    {
        var webhookClient = CachedWebhookClientsStorage.Find(spawnedCharacter.WebhookId);
        if (webhookClient is null)
        {
            await InteractionsHelper.CreateDiscordWebhookAsync((IIntegrationChannel)Context.Channel, spawnedCharacter.CharacterName, spawnedCharacter.CharacterImageLink);
        }
        else
        {
            await webhookClient.ModifyWebhookAsync(w => { w.Name = spawnedCharacter.CharacterName; });
        }
    }

    private async Task UpdateAvatarAsync(ISpawnedCharacter spawnedCharacter, string newAvatarUrl)
    {
        var webhookClient = CachedWebhookClientsStorage.Find(spawnedCharacter.WebhookId);
        if (webhookClient is null)
        {
            await InteractionsHelper.CreateDiscordWebhookAsync((IIntegrationChannel)Context.Channel, spawnedCharacter.CharacterName, newAvatarUrl);
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


    private async Task<(ISpawnedCharacter spawnedCharacter, CachedCharacterInfo cachedCharacter)> FindCharacterAsync(string anyIdentifier, ulong channelId)
    {
        var cachedCharacter = _cacheRepository.CachedCharacters.Find(anyIdentifier, channelId);
        if (cachedCharacter is null)
        {
            throw new UserFriendlyException($"Character **{anyIdentifier}** not found", bold: false);
        }

        var spawnedCharacter = await _charactersDbRepository.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
        if (spawnedCharacter is null)
        {
            throw new UserFriendlyException($"Character **{anyIdentifier}** not found", bold: false);
        }

        return (spawnedCharacter, cachedCharacter);
    }


    private async Task<string> UpdateCharacterMessagesFormatAsync(ISpawnedCharacter spawnedCharacter, string? newFormat)
    {
        ValidateMessagesFormat(newFormat);

        spawnedCharacter.MessagesFormat = newFormat;
        await _db.SaveChangesAsync();

        return $"{MT.OK_SIGN_DISCORD} Messages format for character {spawnedCharacter.GetMention()} {(newFormat is null ? "reset to default value" : "was changed")} successfully:\n" +
               _interactionsMaster.BuildCharacterMessagesFormatDisplayAsync(spawnedCharacter);
    }


    private async Task<string> UpdateCharacterSystemPromptAsync(ISpawnedCharacter spawnedCharacter, string? newPrompt)
    {
        // ValidateMessagesFormat(newFormat);

        if (spawnedCharacter is not IAdoptedCharacter adoptedCharacter)
        {
            throw new UserFriendlyException($"Not available for {spawnedCharacter.GetIntegrationType().GetIcon()}**{spawnedCharacter.GetIntegrationType():G}** characters");
        }

        adoptedCharacter.AdoptedCharacterSystemPrompt = newPrompt;
        await _db.SaveChangesAsync();

        return $"{MT.OK_SIGN_DISCORD} System prompt for character {spawnedCharacter.GetMention()} {(newPrompt is null ? "reset to default value" : "was changed")} successfully:\n" +
               _interactionsMaster.BuildCharacterSystemPromptDisplayAsync(spawnedCharacter);
    }
}
