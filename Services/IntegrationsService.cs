using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterAI;
using CharacterEngineDiscord.Models.Database;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.CharacterHub;
using CharacterEngineDiscord.Models.OpenAI;
using Newtonsoft.Json;
using System.Dynamic;
using System.Text;
using System.Net;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.CommandsService;
using Discord.Commands;

namespace CharacterEngineDiscord.Services
{
    public class IntegrationsService
    {
        /// <summary>
        /// (User ID : [current minute : interactions count])
        /// </summary>
        private readonly Dictionary<ulong, KeyValuePair<int, int>> _watchDog = new();

        internal HttpClient HttpClient { get; } = new();
        internal CharacterAIClient? CaiClient { get; set; }
        internal List<SearchQuery> SearchQueries { get; } = new();

        /// <summary>
        /// Webhook ID : WebhookClient
        /// </summary>
        internal Dictionary<ulong, DiscordWebhookClient> WebhookClients { get; } = new();

        /// <summary>
        /// Stored swiped messages (Webhook ID : AvailableCharacterResponse)
        /// </summary>
        internal Dictionary<ulong, List<AvailableCharacterResponse>> AvailableCharacterResponses { get; } = new();

        /// <summary>
        /// For internal use only
        /// </summary>
        public enum IntegrationType
        {
            CharacterAI,
            OpenAI,
            Empty
        }

        public async Task Initialize()
        {
            HttpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            HttpClient.DefaultRequestHeaders.Add("AcceptEncoding", "gzip, deflate, br");
            HttpClient.DefaultRequestHeaders.Add("UserAgent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36");
            
            bool useCai = ConfigFile.CaiEnabled.Value.ToBool();
            if (useCai)
            {
                CaiClient = new(
                    userToken: ConfigFile.DefaultCaiUserAuthToken.Value!,
                    caiPlusMode: ConfigFile.DefaultCaiPlusMode.Value.ToBool(),
                    customBrowserDirectory: ConfigFile.PuppeteerBrowserDir.Value,
                    customBrowserExecutablePath: ConfigFile.PuppeteerBrowserExe.Value
                );
                await CaiClient.LaunchBrowserAsync(killDuplicates: true);
                AppDomain.CurrentDomain.ProcessExit += (s, args) => CaiClient.KillBrowser();

                Log("CharacterAI client - "); LogGreen("Running\n\n");
            }

            Environment.SetEnvironmentVariable("READY", "1", EnvironmentVariableTarget.Process);
        }

        public DiscordWebhookClient? GetWebhookClient(ulong webhookId, string webhookToken)
        {
            if (!WebhookClients.TryGetValue(webhookId, out DiscordWebhookClient? client))
            {
                try
                {
                    client = new DiscordWebhookClient(webhookId, webhookToken);
                    WebhookClients.TryAdd(webhookId, client);
                }
                catch { return null; }
            }

            return client;
        }

        /// <summary>
        /// Temporary "raw" solution, will be redone into a library later
        /// </summary>
        internal static async Task<OpenAiChatResponse?> CallChatOpenAiAsync(OpenAiChatRequestParams requestParams, HttpClient httpClient)
        {
            // Build data payload
            dynamic content = new ExpandoObject();
            content.frequency_penalty = requestParams.FreqPenalty;
            content.max_tokens = requestParams.MaxTokens;
            content.model = requestParams.Model;
            content.presence_penalty = requestParams.PresencePenalty;
            content.temperature = requestParams.Temperature;
            content.messages = requestParams.Messages.ConvertAll(m => m.ToDict());

            // Getting character response
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestParams.ApiEndpoint)
            {
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {requestParams.ApiToken}" } },
                Content = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json"),
            };

            try
            {
                var response = await httpClient.SendAsync(httpRequestMessage);
                return new(response);
            }
            catch
            {
                return null;
            }
        }

        internal static OpenAiChatRequestParams BuildChatOpenAiRequestPayload(CharacterWebhook characterWebhook, bool isSwipe = false, bool isContinue = false)
        {
            string jailbreakPrompt = characterWebhook.UniversalJailbreakPrompt ?? characterWebhook.Channel.Guild.GuildJailbreakPrompt ?? ConfigFile.DefaultJailbreakPrompt.Value!;
            string fullJailbreakPrompt = $"{jailbreakPrompt.Replace("{{char}}", $"{characterWebhook.Character.Name}")}.  " +
                                         $"Character's name: {characterWebhook.Character.Name}.  " +
                                         $"Character calls {{{{user}}}} by any name introduced by {{{{user}}}}.  " +
                                         $"{characterWebhook.Character.Definition?.Replace("{{char}}", $"{characterWebhook.Character.Name}")}";

            // ALWAYS add jailbreak prompt to the payload
            var messages = new List<OpenAiMessage> { new("system", fullJailbreakPrompt) };
            
            // Count~ tokens
            float currentAmountOfTokens = fullJailbreakPrompt.Length / 3.6f;

            // Create a separate list and fill it with the dialog history in reverse order.
            // Too old messages, these that are out of approximate token limit, will be ignored and deleted later.
            var oldMessages = new List<OpenAiMessage>();
            for (int i = characterWebhook.OpenAiHistoryMessages.Count - 1; i >= 0; i--)
            {
                string msgContent = characterWebhook.OpenAiHistoryMessages[i].Content;
                float tokensInThisMessage = msgContent.Length / 3.6f;
                if ((currentAmountOfTokens + tokensInThisMessage) > 3600f) break;

                // Build history message
                var historyMessage = new OpenAiMessage(characterWebhook.OpenAiHistoryMessages[i].Role, msgContent);
                oldMessages.Add(historyMessage);

                // Update token counter
                currentAmountOfTokens += tokensInThisMessage;
            }

            oldMessages.Reverse(); // restore natural order

            if (isSwipe)
                oldMessages.RemoveAt(oldMessages.Count-1);
            else if (isContinue)
                oldMessages.Add(new("user", "(continue character response from the point where it stopped)"));

            messages.AddRange(oldMessages); // add history message to the payload

            // Build request payload
            var openAiParams = new OpenAiChatRequestParams()
            {
                ApiEndpoint = characterWebhook.PersonalOpenAiApiEndpoint ?? characterWebhook.Channel.Guild.GuildOpenAiApiEndpoint ?? ConfigFile.DefaultOpenAiApiEndpoint.Value!,
                ApiToken = characterWebhook.PersonalOpenAiApiToken ?? characterWebhook.Channel.Guild.GuildOpenAiApiToken ?? ConfigFile.DefaultOpenAiApiToken.Value!,
                UniversalJailbreakPrompt = fullJailbreakPrompt,
                Temperature = characterWebhook.OpenAiTemperature ?? 1.05f,
                FreqPenalty = characterWebhook.OpenAiFreqPenalty ?? 0.9f,
                PresencePenalty = characterWebhook.OpenAiPresencePenalty ?? 0.9f,
                MaxTokens = characterWebhook.OpenAiMaxTokens ?? 200,
                Model = characterWebhook.OpenAiModel!,
                Messages = messages
            };

            return openAiParams;
        }

        internal static async Task<ChubSearchResponse> SearchChubCharactersAsync(ChubSearchParams searchParams, HttpClient client)
        {
            string uri = "https://v2.chub.ai/search?" +
                $"&search={searchParams.Text}" +
                $"&first={searchParams.Amount}" +
                $"&topics={searchParams.Tags}" +
                $"&excludetopics={searchParams.ExcludeTags}" +
                $"&page={searchParams.Page}" +
                $"&sort={searchParams.SortFieldValue}" +
                $"&nsfw={searchParams.AllowNSFW}";
                
            var response = await client.GetAsync(uri);
            string originalQuery = $"{searchParams.Text ?? "no input"}";
            originalQuery += string.IsNullOrWhiteSpace(searchParams.Tags) ? "" : $" (tags: {searchParams.Tags})";

            return new(response, originalQuery);
        }

        internal static async Task<ChubCharacter?> GetChubCharacterInfo(string characterId, HttpClient client)
        {
            string uri = $"https://v2.chub.ai/api/characters/{characterId}?full=true";
            var response = await client.GetAsync(uri);
            var content = await response.Content.ReadAsStringAsync();
            var node = JsonConvert.DeserializeObject<dynamic>(content)?.node;

            try
            {
                return new(node, true);
            }
            catch
            {
                return null;
            }
        }

        internal static async Task<CharacterWebhook?> CreateCharacterWebhookAsync(IntegrationType type, InteractionContext context, Models.Database.Character unsavedCharacter, IntegrationsService integration)
        {
            // Create basic call prefix from two first letters in the character name
            int l = Math.Min(2, unsavedCharacter.Name.Length-1);
            string callPrefix = $"..{unsavedCharacter.Name![..l].ToLower()}"; // => "..ch"

            IIntegrationChannel? discordChannel;
            discordChannel = context.Channel as IIntegrationChannel;
            if (discordChannel is null) return null;

            // replacing with Russian 'о' and 'с', as name "discord" is not allowed for webhooks
            string name = unsavedCharacter.Name.ToLower().Contains("discord") ? unsavedCharacter.Name.Replace('o', 'о').Replace('c', 'с') : unsavedCharacter.Name;

            var image = await TryDownloadImgAsync(unsavedCharacter.AvatarUrl, integration.HttpClient);
            image ??= new MemoryStream(File.ReadAllBytes($"{EXE_DIR}{SC}storage{SC}default_avatar.png"));

            IWebhook? channelWebhook;
            try
            {
                channelWebhook = await discordChannel.CreateWebhookAsync(name, image);
            }
            catch (Exception e)
            {
                await context.Interaction.FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to create webhook: {e.Message}".ToInlineEmbed(Color.Red));
                return null;
            }

            var db = new StorageContext();
            try
            {
                var channel = await FindOrStartTrackingChannelAsync(context.Channel.Id, context.Guild.Id, db);

                string? caiHistoryId, openAiModel, openAiEndpoint;
                caiHistoryId = openAiModel = openAiEndpoint = null;

                float? openAiFreqPenalty, openAiPresPenalty, openAiTemperature;
                openAiFreqPenalty = openAiPresPenalty = openAiTemperature = null;

                int? openAiMaxTokens = null;

                if (type is IntegrationType.CharacterAI)
                {
                    if (integration.CaiClient is null) return null;

                    var caiToken = channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;
                    if (string.IsNullOrWhiteSpace(caiToken)) return null;

                    var plusMode = channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();

                    caiHistoryId = await integration.CaiClient.CreateNewChatAsync(unsavedCharacter.Id, customAuthToken: caiToken, customPlusMode: plusMode);
                    if (caiHistoryId is null) return null;
                }
                else if (type is IntegrationType.OpenAI)
                {
                    openAiModel = channel.Guild.GuildOpenAiModel ?? ConfigFile.DefaultOpenAiModel.Value;
                    openAiEndpoint = channel.Guild.GuildOpenAiApiEndpoint ?? ConfigFile.DefaultOpenAiApiEndpoint.Value;
                    openAiFreqPenalty = 0.9f;
                    openAiPresPenalty = 0.9f;
                    openAiTemperature = 1.05f;
                    openAiMaxTokens = 200;
                }

                var character = await FindOrStartTrackingCharacterAsync(unsavedCharacter, db);
                var characterWebhook = (await db.CharacterWebhooks.AddAsync(new CharacterWebhook()
                {
                    Id = channelWebhook.Id,
                    WebhookToken = channelWebhook.Token,
                    CallPrefix = callPrefix,
                    ReferencesEnabled = false,
                    SwipesEnabled = true,
                    CrutchEnabled = type is not IntegrationType.CharacterAI,
                    ResponseDelay = 1,
                    MessagesFormat = null,
                    UniversalJailbreakPrompt = null,
                    IntegrationType = type,
                    ReplyChance = 0,
                    CaiActiveHistoryId = caiHistoryId,
                    OpenAiModel = openAiModel,
                    OpenAiFreqPenalty = openAiFreqPenalty,
                    OpenAiPresencePenalty = openAiPresPenalty,
                    OpenAiTemperature = openAiTemperature,
                    OpenAiMaxTokens = openAiMaxTokens,
                    CharacterId = character.Id,
                    ChannelId = channel.Id,
                    LastCallTime = DateTime.UtcNow,
                })).Entity;

                if (type is not IntegrationType.CharacterAI)
                    db.OpenAiHistoryMessages.Add(new() { CharacterWebhookId = channelWebhook.Id, Content = character.Greeting, Role = "assistant" });

                await db.SaveChangesAsync();
                return characterWebhook;
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                await TryToReportInLogsChannel(context.Client, "Exception", "Failed to spawn character", e.ToString(), Color.Red, true);
                await channelWebhook.DeleteAsync();

                return null;
            }
        }

        internal static SearchQueryData SearchQueryDataFromCaiResponse(CharacterAI.Models.SearchResponse response)
        {
            var characters = new List<Character>();

            foreach(var c in response.Characters)
            {
                var cc = CharacterFromCaiCharacterInfo(c);
                if (cc is not null) characters.Add(cc);
            }

            return new(characters.ToList(), response.OriginalQuery, IntegrationType.CharacterAI) { ErrorReason = response.ErrorReason };
        }

        internal static SearchQueryData SearchQueryDataFromChubResponse(ChubSearchResponse response)
        {
            var characters = new List<Character>();

            foreach (var c in response.Characters)
            {
                var cc = CharacterFromChubCharacterInfo(c);
                if (cc is not null) characters.Add(cc);
            }

            return new(characters.ToList(), response.OriginalQuery, IntegrationType.OpenAI) { ErrorReason = response.ErrorReason };
        }

        internal static Models.Database.Character? CharacterFromCaiCharacterInfo(CharacterAI.Models.Character caiCharacter)
        {
            if (caiCharacter.IsEmpty) return null;

            return new()
            {
                Id = caiCharacter.Id!,
                Tgt = caiCharacter.Tgt!,
                Name = caiCharacter.Name!,
                Title = caiCharacter.Title,
                Greeting = caiCharacter.Greeting!,
                Description = caiCharacter.Description,
                AuthorName = caiCharacter.Author,
                AvatarUrl = caiCharacter.AvatarUrlFull ?? caiCharacter.AvatarUrlMini,
                ImageGenEnabled = caiCharacter.ImageGenEnabled ?? false,
                Interactions = caiCharacter.Interactions ?? 0,
                Stars = null,
                Definition = null
            };
        }

        internal static Character? CharacterFromChubCharacterInfo(ChubCharacter? chubCharacter)
        {
            if (chubCharacter is null) return null;

            try
            {
                return new()
                {
                    Id = chubCharacter.FullPath,
                    Tgt = null,
                    Name = chubCharacter.Name,
                    Title = chubCharacter.TagLine,
                    Greeting = chubCharacter.FirstMessage ?? "",
                    Description = chubCharacter.Description,
                    AuthorName = chubCharacter.AuthorName,
                    AvatarUrl = $"https://avatars.charhub.io/avatars/{chubCharacter.FullPath}/avatar.webp",
                    ImageGenEnabled = false,
                    Interactions = chubCharacter.ChatsCount,
                    Stars = chubCharacter.StarCount,
                    Definition = $"{{{{char}}}}'s personality: {chubCharacter.Personality}  " +
                                 $"Scenario of roleplay: {chubCharacter.Scenario}  " +
                                 $"Example conversations between {{{{char}}}} and {{{{user}}}}: {chubCharacter.ExampleDialogs}  "
                };
            }
            catch
            {
                return null;
            }
        }

        internal static async Task<bool> UserIsBannedCheckOnly(ulong userId)
            => (await new StorageContext().BlockedUsers.FindAsync(userId)) is not null;

        internal async Task<bool> UserIsBanned(SocketCommandContext context)
        {
            var db = new StorageContext();

            ulong currUserId = context.Message.Author.Id;
            var blockedUser = await db.BlockedUsers.FindAsync(currUserId);
            if (blockedUser is not null) return true;

            int currentMinuteOfDay = context.Message.CreatedAt.Minute + context.Message.CreatedAt.Hour * 60;

            // Start watching for user
            if (!_watchDog.ContainsKey(currUserId))
                _watchDog.Add(currUserId, new(-1, 0)); // user id : (current minute : count)

            // Drop + update user stats if he replies in another minute
            if (_watchDog[currUserId].Key != currentMinuteOfDay)
                _watchDog[currUserId] = new(currentMinuteOfDay, 0);

            // Update interactions count within current minute
            _watchDog[currUserId] = new(_watchDog[currUserId].Key, _watchDog[currUserId].Value + 1);

            int rateLimit = int.Parse(ConfigFile.RateLimit.Value!);

            if (_watchDog[currUserId].Value == rateLimit - 2)
                await context.Message.ReplyAsync(embed: $"{WARN_SIGN_DISCORD} {context.User.Mention} Warning! If you proceed to call the bot so fast, you'll be blocked from using it.".ToInlineEmbed(Color.Orange));
            else if (_watchDog[currUserId].Value > rateLimit)
            {
                await db.BlockedUsers.AddAsync(new() { Id = currUserId, From = DateTime.UtcNow, Hours = 24 });

                await db.SaveChangesAsync();
                _watchDog.Remove(currUserId);

                await TryToReportInLogsChannel(context.Client, title: $":eyes: Notification",
                                                               desc: $"Server: **{context.Guild?.Name} ({context.Guild?.Id})** owned by **{context.Guild?.Owner?.Username} ({context.Guild?.OwnerId})**\nUser **{context.Message?.Author?.Username} ({context.Message?.Author?.Id})** hit the rate limit and was blocked",
                                                               content: null,
                                                               color: Color.LightOrange,
                                                               error: false);

                await context.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} {context.User.Mention}, you were calling the characters way too fast and have exceeded the rate limit.\nYou will not be able to use the bot in next 24 hours.".ToInlineEmbed(Color.Red));
                return true;
            }
            return false;
        }

        internal async Task<bool> UserIsBanned(SocketReaction reaction, DiscordSocketClient client)
        {
            var db = new StorageContext();

            ulong currUserId = reaction.User.Value.Id;
            var blockedUser = await db.BlockedUsers.FindAsync(currUserId);
            if (blockedUser is not null) return true;

            int currentMinuteOfDay = DateTime.UtcNow.Minute + DateTime.UtcNow.Hour * 60;

            // Start watching for user
            if (!_watchDog.ContainsKey(currUserId))
                _watchDog.Add(currUserId, new(-1, 0)); // user id : (current minute : count)

            // Drop + update user stats if he replies in another minute
            if (_watchDog[currUserId].Key != currentMinuteOfDay)
                _watchDog[currUserId] = new(currentMinuteOfDay, 0);

            // Update interactions count within current minute
            _watchDog[currUserId] = new(_watchDog[currUserId].Key, _watchDog[currUserId].Value + 1);

            int rateLimit = int.Parse(ConfigFile.RateLimit.Value!);

            if (_watchDog[currUserId].Value == rateLimit - 1)
                await reaction.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} {reaction.User.Value?.Mention} Warning! If you proceed to call the bot so fast, you'll be blocked from using it.".ToInlineEmbed(Color.Orange));
            else if (_watchDog[currUserId].Value > rateLimit)
            {
                await db.BlockedUsers.AddAsync(new() { Id = currUserId, From = DateTime.UtcNow, Hours = 24 });

                await db.SaveChangesAsync();
                _watchDog.Remove(currUserId);

                var currentChannel = await client.GetChannelAsync(reaction.Channel.Id) as SocketTextChannel;
                await TryToReportInLogsChannel(client, title: $":eyes: Notification",
                                                       desc: $"Server: **{currentChannel?.Guild?.Name} ({currentChannel?.Guild?.Id})** owned by **{currentChannel?.Guild?.Owner?.Username} ({currentChannel?.Guild?.OwnerId})**\nUser **{reaction.User.Value.Username}** ({reaction.User.Value.Id}) hit the rate limit and was blocked",
                                                       content: null,
                                                       color: Color.LightOrange,
                                                       error: false);

                await reaction.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} {reaction.User.Value.Mention}, you were calling the characters way too fast and have exceeded the rate limit.\nYou will not be able to use the bot in next 24 hours.".ToInlineEmbed(Color.Red));
                return true;
            }
            return false;
        }

        public void WatchDogClear()
        {
            _watchDog.Clear();
        }

        // Shortcuts

        internal static Embed FailedToSetCharacterEmbed()
            => $"{WARN_SIGN_DISCORD} Failed to set a character".ToInlineEmbed(Color.Red);

        internal static Embed SuccessEmbed()
            => $"{OK_SIGN_DISCORD} Success".ToInlineEmbed(Color.Green);
    }
}
