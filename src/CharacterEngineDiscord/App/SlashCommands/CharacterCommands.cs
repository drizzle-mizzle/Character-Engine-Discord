using System.Text;
using CharacterAi.Client.Exceptions;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Modules.Abstractions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;
using MT = CharacterEngine.App.Helpers.Discord.MessagesTemplates;

namespace CharacterEngine.App.SlashCommands;


[ValidateChannelPermissions]
[Group("character", "Basic characters commands")]
public class CharacterCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordSocketClient;
    private const string ANY_IDENTIFIER_DESC = "Character call prefix or User ID (Webhook ID)";
    private const string NSFW_REQUIRED = "Sorry, but NSFW characters can be spawned only in age restricted channels. Please, mark channel as NSFW and try again.";

    public CharacterCommands(AppDbContext db, DiscordSocketClient discordSocketClient)
    {
        _db = db;
        _discordSocketClient = discordSocketClient;
    }


    [ValidateAccessLevel(AccessLevels.Manager)]
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

        var guildIntegration = await DatabaseHelper.GetGuildIntegrationAsync(Context.Guild.Id, integrationType);
        if (guildIntegration is null)
        {
            throw new UserFriendlyException($"You have to setup **{integrationType:G}** intergration for this server first!", bold: false);
        }

        ISearchModule searchModule;
        if (!guildIntegration.IsChatOnly)
        {
            searchModule = integrationType.GetSearchModule();
        }
        else if (useCatalog is CharacterSourceType sourceType)
        {
            searchModule = sourceType.GetSearchModule();
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
                throw new UserFriendlyException($"{integrationType.GetIcon()} No characters were found by query **\"{searchQuery}\"**", bold: false);
            }

            var newSq = new SearchQuery(originalResponse.Id, Context.User.Id, searchQuery!, characters, integrationType);
            MemoryStorage.SearchQueries.Add(newSq);

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

            var newSpawnedCharacter = await InteractionsHelper.SpawnCharacterAsync(Context.Channel.Id, characterAdapter.ToCommonCharacter());

            var embed = MH.BuildCharacterDescriptionCard(newSpawnedCharacter, justSpawned: true);
            var modifyOriginalResponseAsync = ModifyOriginalResponseAsync(msg => { msg.Embed = embed; });

            await newSpawnedCharacter.SendGreetingAsync(((SocketGuildUser)Context.User).DisplayName, threadId: isThread ? channel.Id : null);
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


    [ValidateAccessLevel(AccessLevels.Manager)]
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

        var updateSpawnedCharacterAsync = DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);
        var message = $"{MT.OK_SIGN_DISCORD} Chat with {spawnedCharacter.GetMention()} reset successfully";

        var followupAsync = FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false));
        var greetingMessageId = await spawnedCharacter.SendGreetingAsync(((IGuildUser)Context.User).DisplayName);

        var cachedCharacter = MemoryStorage.CachedCharacters.Find(spawnedCharacter.Id)!;
        cachedCharacter.WideContextLastMessageId = greetingMessageId;

        await updateSpawnedCharacterAsync;
        await followupAsync;
    }


    [ValidateAccessLevel(AccessLevels.Manager)]
    [SlashCommand("remove", "Remove character")]
    public async Task RemoveCharacter([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier)
    {
        await DeferAsync();

        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var spawnedCharacter = scc.spawnedCharacter;

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

        var message = $"{MT.OK_SIGN_DISCORD} Character {spawnedCharacter.GetMention()} removed successfully";
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: false));

        MemoryStorage.CachedCharacters.Remove(spawnedCharacter.Id);
        MemoryStorage.CachedWebhookClients.Remove(spawnedCharacter.WebhookId);
        await deleteSpawnedCharacterAsync;
    }


    [SlashCommand("hunted-users", "Make character hunt certain user or another character")]
    public async Task HuntedUsers(UserAction action, [Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, IGuildUser? user = null, string? userIdOrCharacterCallPrefix = null)
    {
        if (action is not UserAction.show)
        {
            await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.Manager, (SocketGuildUser)Context.User);
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
            var otherCharacter = MemoryStorage.CachedCharacters.Find(userIdOrCharacterCallPrefix!, Context.Channel.Id);
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

    [SlashCommand("messages-format", "Messages format")]
    public async Task MessagesFormat(MessagesFormatAction action, [Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, string? newFormat = null, bool hide = false)
    {
        if (action is not MessagesFormatAction.show)
        {
            await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.Manager, (SocketGuildUser)Context.User);
        }

        await DeferAsync(ephemeral: hide);
        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);

        string message = default!;

        switch (action)
        {
            case MessagesFormatAction.show:
            {
                message = await InteractionsHelper.GetCharacterMessagesFormatAsync(scc.spawnedCharacter);
                break;
            }
            case MessagesFormatAction.update:
            {
                if (newFormat is null)
                {
                    throw new UserFriendlyException("Specify the new-format parameter");
                }

                message = await InteractionsHelper.UpdateCharacterMessagesFormatAsync(scc.spawnedCharacter, newFormat);
                break;
            }
            case MessagesFormatAction.resetDefault:
            {
                message = await InteractionsHelper.UpdateCharacterMessagesFormatAsync(scc.spawnedCharacter, null);
                break;
            }
        }

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


    [ValidateAccessLevel(AccessLevels.Manager)]
    [SlashCommand("toggle", "Enable/disable feature")]
    public async Task Toggle([Summary(description: ANY_IDENTIFIER_DESC)] string anyIdentifier, TogglableSettings feature)
    {
        await DeferAsync();

        var scc = await FindCharacterAsync(anyIdentifier, Context.Channel.Id);
        var spawnedCharacter = scc.spawnedCharacter;

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

        var message = $"{MT.OK_SIGN_DISCORD} **{featureName}** for character {spawnedCharacter.GetMention()} was successfully changed to **{newValue}**";
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


    [ValidateAccessLevel(AccessLevels.Manager)]
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

        await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

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
        var webhookClient = MemoryStorage.CachedWebhookClients.Find(spawnedCharacter.WebhookId);
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
        var webhookClient = MemoryStorage.CachedWebhookClients.Find(spawnedCharacter.WebhookId);
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


    private static async Task<(ISpawnedCharacter spawnedCharacter, CachedCharacterInfo cachedCharacter)> FindCharacterAsync(string anyIdentifier, ulong channelId)
    {
        var cachedCharacter = MemoryStorage.CachedCharacters.Find(anyIdentifier, channelId);
        if (cachedCharacter is null)
        {
            throw new UserFriendlyException($"Character **{anyIdentifier}** not found", bold: false);
        }

        var spawnedCharacter = await DatabaseHelper.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
        if (spawnedCharacter is null)
        {
            throw new UserFriendlyException($"Character **{anyIdentifier}** not found", bold: false);
        }

        return (spawnedCharacter, cachedCharacter);
    }
}
