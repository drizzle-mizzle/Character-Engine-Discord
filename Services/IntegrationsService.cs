﻿using Discord;
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
using CharacterEngineDiscord.Models.KoboldAI;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using CharacterEngineDiscord.Services.AisekaiIntegration;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;

namespace CharacterEngineDiscord.Services
{
    public class IntegrationsService
    {
        /// <summary>
        /// (User ID : [current minute : interactions count])
        /// </summary>
        private readonly Dictionary<ulong, KeyValuePair<int, int>> _watchDog = new();
        internal ulong MessagesSent { get; set; } = 0;
        internal List<SearchQuery> SearchQueries { get; } = new();
        internal SemaphoreSlim SearchQueriesLock { get; } = new(1, 1);

        internal HttpClient ImagesHttpClient { get; } = new();
        internal HttpClient ChubAiHttpClient { get; } = new();
        internal HttpClient CommonHttpClient { get; } = new();

        internal AisekaiClient AisekaiClient { get; } = new();
        internal CharacterAIClient? CaiClient { get; set; }

        /// <summary>
        /// Webhook ID : WebhookClient
        /// </summary>
        internal Dictionary<ulong, DiscordWebhookClient> WebhookClients { get; } = new();

        /// <summary>
        /// Stored swiped messages (Character-webhook ID : LastCharacterCall)
        /// </summary>
        internal Dictionary<ulong, LastCharacterCall> Conversations { get; } = new();

        /// <summary>
        /// For internal use only
        /// </summary>
        public enum IntegrationType
        {
            CharacterAI,
            Aisekai,
            OpenAI,
            KoboldAI,
            HordeKoboldAI,
            Empty
        }


        public void Initialize()
        {
            ImagesHttpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            ImagesHttpClient.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
            ImagesHttpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            ImagesHttpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            ImagesHttpClient.DefaultRequestHeaders.Add("User-Agent", ConfigFile.DefaultHttpClientUA.Value);
            ImagesHttpClient.Timeout = new(0, 1, 0);

            CommonHttpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            CommonHttpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            CommonHttpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            CommonHttpClient.DefaultRequestHeaders.Add("User-Agent", ConfigFile.DefaultHttpClientUA.Value);
            CommonHttpClient.Timeout = new(0, 3, 0);

            if (ConfigFile.CaiEnabled.Value.ToBool())
            {
                CaiClient = new(
                    customBrowserDirectory: ConfigFile.PuppeteerBrowserDir.Value,
                    customBrowserExecutablePath: ConfigFile.PuppeteerBrowserExe.Value
                );
                CaiClient.LaunchBrowser(killDuplicates: true);
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
        internal static async Task<OpenAiChatResponse?> SendOpenAiRequestAsync(OpenAiChatRequestParams requestParams, HttpClient httpClient)
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
                using var response = await httpClient.SendAsync(httpRequestMessage);
                return new(response);
            }
            catch
            {
                return null;
            }
        }

        internal static async Task<KoboldAiResponse?> SendKoboldAiRequestAsync(string characterName, KoboldAiRequestParams requestParams, HttpClient httpClient, bool continueRequest)
        {
            string prompt = "";
            foreach (var msg in requestParams.Messages)
                prompt += $"{msg.Role}{msg.Content}";

            if (!continueRequest)
                prompt += $"\n<{characterName}>\n";

            // Build data payload
            dynamic content = new ExpandoObject();
            content.max_context_length = requestParams.MaxContextLength;
            content.max_length = requestParams.MaxLength;
            content.prompt = prompt;
            content.rep_pen = requestParams.RepetitionPenalty;
            content.rep_pen_slope = requestParams.RepetitionPenaltySlope;
            content.singleline = requestParams.SingleLine;
            content.temperature = requestParams.Temperature;
            content.tfs = requestParams.TailFreeSampling;
            content.top_a = requestParams.TopA;
            content.top_k = requestParams.TopK;
            content.top_P = requestParams.TopP;
            content.typical = requestParams.TypicalSampling;

            // Getting character response
            string url = $"{requestParams.ApiEndpoint.Trim('/')}/v1/generate";
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json"),
            };

            try
            {
                using var response = await httpClient.SendAsync(httpRequestMessage);
                return new(response);
            }
            catch
            {
                return null;
            }
        }

        internal static async Task<HordeKoboldAiResponse?> SendHordeKoboldAiRequestAsync(string characterName, HordeKoboldAiRequestParams requestParams, HttpClient httpClient, bool continueRequest)
        {
            string prompt = "";
            foreach (var msg in requestParams.KoboldAiSettings.Messages)
                prompt += $"{msg.Role}{msg.Content}";

            if (!continueRequest)
                prompt += $"\n<{characterName}>\n";

            // Build data payload
            dynamic koboldParams = new ExpandoObject();
            koboldParams.max_context_length = requestParams.KoboldAiSettings.MaxContextLength;
            koboldParams.max_length = requestParams.KoboldAiSettings.MaxLength;
            koboldParams.rep_pen = requestParams.KoboldAiSettings.RepetitionPenalty;
            koboldParams.rep_pen_slope = requestParams.KoboldAiSettings.RepetitionPenaltySlope;
            koboldParams.singleline = requestParams.KoboldAiSettings.SingleLine;
            koboldParams.temperature = requestParams.KoboldAiSettings.Temperature;
            koboldParams.tfs = requestParams.KoboldAiSettings.TailFreeSampling;
            koboldParams.top_a = requestParams.KoboldAiSettings.TopA;
            koboldParams.top_k = requestParams.KoboldAiSettings.TopK;
            koboldParams.top_P = requestParams.KoboldAiSettings.TopP;
            koboldParams.typical = requestParams.KoboldAiSettings.TypicalSampling;

            dynamic content = new ExpandoObject();
            content.models = new string[] { requestParams.Model };
            (content as IDictionary<string, object>)!["params"] = koboldParams;
            content.use_default_badwordsids = true;
            content.prompt = prompt;

            // Getting character response
            string url = "https://horde.koboldai.net/api/v2/generate/text/async";
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, url);

            httpRequestMessage.Headers.Add("Apikey", requestParams.Token);
            httpRequestMessage.Content = new StringContent(JsonConvert.SerializeObject(koboldParams), Encoding.UTF8, "application/json");

            try
            {
                using var response = await httpClient.SendAsync(httpRequestMessage);
                return new(response);
            }
            catch
            {
                return null;
            }
        }

        internal static OpenAiChatRequestParams BuildChatOpenAiRequestPayload(CharacterWebhook characterWebhook, bool isSwipe = false, bool isContinue = false)
        {
            string jailbreakPrompt = characterWebhook.PersonalJailbreakPrompt ?? characterWebhook.Channel.Guild.GuildJailbreakPrompt ?? ConfigFile.DefaultJailbreakPrompt.Value!;
            string fullSystemPrompt = $"{jailbreakPrompt.Replace("{{char}}", $"{characterWebhook.Character.Name}")}  " +
                                         $"{characterWebhook.Character.Definition?.Replace("{{char}}", $"{characterWebhook.Character.Name}")}";

            // Always add system prompt to the beginning of payload
            var messages = new List<OpenAiMessage> { new("system", fullSystemPrompt) };
            
            // Count~ tokens
            float currentAmountOfTokens = fullSystemPrompt.Length / 3.6f;

            // Create a separate list and fill it with the dialog history in reverse order.
            // Too old messages, these that are out of approximate token limit, will be ignored and deleted later.
            var oldMessages = new List<OpenAiMessage>();
            for (int i = characterWebhook.StoredHistoryMessages.Count - 1; i >= 0; i--)
            {
                string msgContent = characterWebhook.StoredHistoryMessages[i].Content;
                float tokensInThisMessage = msgContent.Length / 3.6f;
                if ((currentAmountOfTokens + tokensInThisMessage) > 3600f) break;

                // Build history message
                var historyMessage = new OpenAiMessage(characterWebhook.StoredHistoryMessages[i].Role, msgContent);
                oldMessages.Add(historyMessage);

                // Update token counter
                currentAmountOfTokens += tokensInThisMessage;
            }

            oldMessages.Reverse(); // restore natural order

            if (isSwipe)
                oldMessages.RemoveAt(oldMessages.Count-1); // delete last for swipe
            else if (isContinue)
                oldMessages.Add(new("user", "(continue character response from the point where it stopped)"));

            messages.AddRange(oldMessages);

            // Build request payload
            var openAiParams = new OpenAiChatRequestParams()
            {
                ApiEndpoint = characterWebhook.PersonalApiEndpoint ?? characterWebhook.Channel.Guild.GuildOpenAiApiEndpoint ?? "https://api.openai.com/v1/chat/completions",
                ApiToken = characterWebhook.PersonalApiToken ?? characterWebhook.Channel.Guild.GuildOpenAiApiToken ?? "",
                Temperature = characterWebhook.GenerationTemperature ?? 1.05f,
                FreqPenalty = characterWebhook.GenerationFreqPenaltyOrRepetitionSlope ?? 0.9f,
                PresencePenalty = characterWebhook.GenerationPresenceOrRepetitionPenalty ?? 0.9f,
                MaxTokens = characterWebhook.GenerationMaxTokens ?? 200,
                Model = characterWebhook.PersonalApiModel!,
                Messages = messages
            };

            return openAiParams;
        }

        internal static KoboldAiRequestParams BuildKoboldAiRequestPayload(CharacterWebhook characterWebhook, bool isSwipe = false)
        {
            string jailbreakPrompt = characterWebhook.PersonalJailbreakPrompt ?? characterWebhook.Channel.Guild.GuildJailbreakPrompt ?? ConfigFile.DefaultJailbreakPrompt.Value!;
            string fullSystemPrompt = $"[SYSTEM INFO] \n{jailbreakPrompt.Replace("{{char}}", $"{characterWebhook.Character.Name}")} \n" +
                                      $"{characterWebhook.Character.Definition?.Replace("{{char}}", $"{characterWebhook.Character.Name}")} \n" +
                                      $"[DIALOGUE HISTORY] \n<START> \n";

            // Always add system prompt to the beginning of payload
            var messages = new List<KoboldAiMessage> { new("", fullSystemPrompt) };
            float currentAmountOfTokens = fullSystemPrompt.Length / 4f;

            // Create a separate list and fill it with the dialog history in reverse order.
            // Too old messages, these that are out of approximate token limit, will be ignored and deleted later.
            var oldMessages = new List<KoboldAiMessage>();
            for (int i = characterWebhook.StoredHistoryMessages.Count - 1; i >= 0; i--)
            {
                string msgContent = characterWebhook.StoredHistoryMessages[i].Content;
                float tokensInThisMessage = msgContent.Length / 4f;
                float allTokens = currentAmountOfTokens + tokensInThisMessage;
                float limitTokens = characterWebhook.GenerationContextSizeTokens ?? 4000f;
                if (allTokens > limitTokens) break;

                // Build history message
                var historyMessage = new KoboldAiMessage(characterWebhook.StoredHistoryMessages[i].Role, msgContent);
                oldMessages.Add(historyMessage);

                // Update token counter
                currentAmountOfTokens += tokensInThisMessage;
            }

            oldMessages.Reverse(); // restore natural order

            if (isSwipe)
            {
                oldMessages.RemoveAt(oldMessages.Count - 1); // delete last for swipe
            }

            messages.AddRange(oldMessages);

            // Build request payload
            var koboldAiParams = new KoboldAiRequestParams()
            {
                ApiEndpoint = characterWebhook.PersonalApiEndpoint ?? characterWebhook.Channel.Guild.GuildKoboldAiApiEndpoint ?? "",
                Temperature = characterWebhook.GenerationTemperature ?? 1.07f,
                RepetitionPenalty = characterWebhook.GenerationPresenceOrRepetitionPenalty ?? 1.05f,
                TopP = characterWebhook.GenerationTopP ?? 1f,
                TopA = characterWebhook.GenerationTopA ?? 0f,
                TopK = characterWebhook.GenerationTopK ?? 100,
                TypicalSampling = characterWebhook.GenerationTypicalSampling ?? 1,
                TailFreeSampling = characterWebhook.GenerationTailfreeSampling ?? 0.93f,
                RepetitionPenaltySlope = characterWebhook.GenerationFreqPenaltyOrRepetitionSlope ?? 0.8f,
                SingleLine = false,
                MaxLength = characterWebhook.GenerationMaxTokens ?? 200,
                MaxContextLength = characterWebhook.GenerationContextSizeTokens ?? 4096,
                Messages = messages
            };

            return koboldAiParams;
        }

        internal static HordeKoboldAiRequestParams BuildHordeKoboldAiRequestPayload(CharacterWebhook characterWebhook, bool isSwipe = false)
        {
            var hordeParams = new HordeKoboldAiRequestParams()
            {
                Token = characterWebhook.PersonalApiToken ?? characterWebhook.Channel.Guild.GuildHordeApiToken!,
                Model = characterWebhook.PersonalApiModel ?? characterWebhook.Channel.Guild.GuildHordeModel!,
                KoboldAiSettings = BuildKoboldAiRequestPayload(characterWebhook, isSwipe)
            };

            return hordeParams;
        }

        internal static async Task<ChubSearchResponse?> SearchChubCharactersAsync(ChubSearchParams searchParams, HttpClient client)
        {
            string uri = "https://api.chub.ai/search?" +
                $"search={searchParams.Text}" +
                $"&first={searchParams.Amount}" +
                $"&topics={searchParams.Tags}" +
                $"&excludetopics={searchParams.ExcludeTags}" +
                $"&page={searchParams.Page}" +
                $"&sort={searchParams.SortFieldValue}" +
                $"&nsfw={searchParams.AllowNSFW}";

            try
            {
                using var response = await client.GetAsync(uri);
                string originalQuery = $"{searchParams.Text ?? "no input"}";
                originalQuery += string.IsNullOrWhiteSpace(searchParams.Tags) ? "" : $" (tags: {searchParams.Tags})";

                return new(response, originalQuery);
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                return null;
            }
        }

        internal static async Task<ChubCharacter?> GetChubCharacterInfoAsync(string characterId, HttpClient client)
        {
            string url = $"https://api.chub.ai/api/characters/{characterId}?full=true";

            try
            {
                var content = await client.GetStringAsync(url);
                LogGreen(content);
                var node = JsonConvert.DeserializeObject<dynamic>(content)?.node;
                return new(node, true);
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                return null;
            }
        }

        internal async Task<CharacterWebhook?> CreateCharacterWebhookAsync(IntegrationType type, InteractionContext context, Models.Database.Character unsavedCharacter, IntegrationsService integration, bool fromChub)
        {
            if (context.Channel is not IIntegrationChannel discordChannel) return null;

            // Create basic call prefix from two first letters in the character name
            int l = Math.Min(2, unsavedCharacter.Name.Length-1);
            string callPrefix = $"..{unsavedCharacter.Name![..l].ToLower()}"; // => "..ch"

            var db = new StorageContext();

            IWebhook? channelWebhook;
            try
            {   // replacing with Russian 'о' and 'с', as name "discord" is not allowed for webhooks
                string name = unsavedCharacter.Name.ToLower().Contains("discord") ? unsavedCharacter.Name.Replace('o', 'о').Replace('c', 'с') : unsavedCharacter.Name;
                channelWebhook = await discordChannel.CreateWebhookAsync(name);
            }
            catch (Exception e)
            {
                await context.Interaction.FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to create character webhook: {e.Message}".ToInlineEmbed(Color.Red));
                return null;
            }

            try
            {
                var channel = await FindOrStartTrackingChannelAsync(context.Channel.Id, context.Guild.Id, db);
                var character = await FindOrStartTrackingCharacterAsync(unsavedCharacter, db);

                string? historyId = null;

                if (fromChub)
                {
                    var chubCharacterFull = await GetChubCharacterInfoAsync(unsavedCharacter.Id, ChubAiHttpClient);
                    unsavedCharacter = CharacterFromChubCharacterInfo(chubCharacterFull)!;
                }

                if (type is IntegrationType.CharacterAI)
                {
                    if (integration.CaiClient is null) return null;

                    string? caiToken = channel.Guild.GuildCaiUserToken;
                    if (string.IsNullOrWhiteSpace(caiToken)) return null;

                    bool plusMode = channel.Guild.GuildCaiPlusMode ?? false;

                    var info = await integration.CaiClient.GetInfoAsync(character.Id ?? string.Empty, customAuthToken: caiToken, customPlusMode: plusMode);
                    character.Tgt = info.Tgt;

                    historyId = await integration.CaiClient.CreateNewChatAsync(character.Id ?? string.Empty, customAuthToken: caiToken, customPlusMode: plusMode);
                    if (historyId is null) return null;
                }
                else if (type is IntegrationType.Aisekai)
                {
                    var authToken = channel.Guild.GuildAisekaiAuthToken;
                    if (string.IsNullOrWhiteSpace(authToken)) return null;

                    var response = await integration.AisekaiClient.GetChatInfoAsync(authToken, unsavedCharacter.Id);
                    if (response.Code == 401)
                    {
                        string? newAuthToken = await UpdateGuildAisekaiAuthTokenAsync(channel.Guild.Id, channel.Guild.GuildAisekaiRefreshToken ?? string.Empty);
                        if (newAuthToken is null)
                            return null;
                        else
                            return await CreateCharacterWebhookAsync(type, context, unsavedCharacter, integration, fromChub);
                    }
                    else if (!response.IsSuccessful)
                    {
                        throw new($"Aisekai GetChatInfo()\nCode: {response.Code}\nError: {response.ErrorReason}");
                    }

                    historyId = response.ChatId!;
                    character.Greeting = response.GreetingMessage!;

                    bool tim = await integration.AisekaiClient.PatchToggleInitMessageAsync(authToken, historyId, false);
                    if (!tim) throw new($"Aisekai PatchToggleInitMessageAsync()");
                }
                else if (type is IntegrationType.OpenAI)
                {
                    db.StoredHistoryMessages.Add(new() { CharacterWebhookId = channelWebhook.Id, Role = "assistant", Content = character.Greeting });
                }
                else if (type is IntegrationType.KoboldAI || type is IntegrationType.HordeKoboldAI)
                {
                    db.StoredHistoryMessages.Add(new() { CharacterWebhookId = channelWebhook.Id, Role = $"\n<{character.Name}>\n", Content = character.Greeting });
                }

                var characterWebhook = (await db.CharacterWebhooks.AddAsync(new CharacterWebhook()
                {
                    Id = channelWebhook.Id,
                    WebhookToken = channelWebhook.Token,
                    CallPrefix = callPrefix,
                    ReferencesEnabled = false,
                    SwipesEnabled = true,
                    StopBtnEnabled = true,
                    CrutchEnabled = type is not IntegrationType.CharacterAI && type is not IntegrationType.Aisekai,
                    FromChub = fromChub,
                    ResponseDelay = 1,
                    IntegrationType = type,
                    ReplyChance = 0,
                    ActiveHistoryID = historyId,
                    CharacterId = character.Id ?? string.Empty,
                    ChannelId = channel.Id,
                    LastCallTime = DateTime.UtcNow,
                    MessagesSent = 1
                })).Entity;

                await TryToSaveDbChangesAsync(db);
                return characterWebhook;
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                TryToReportInLogsChannel(context.Client, "Exception", $"Failed to spawn a character:\n```cs\n{e}\n```", null, Color.Red, true);

                if (channelWebhook is not null)
                    try { await channelWebhook.DeleteAsync(); } catch { }

                return null;
            }
        }

        internal static SearchQueryData SearchQueryDataFromCaiResponse(CharacterAI.Models.SearchResponse response)
        {
            var characters = new List<Models.Database.Character>();

            foreach(var c in response.Characters)
            {
                var cc = CharacterFromCaiCharacterInfo(c);
                if (cc is not null) characters.Add(cc);
            }

            return new(characters.ToList(), response.OriginalQuery, IntegrationType.CharacterAI) { ErrorReason = response.ErrorReason };
        }

        internal static SearchQueryData SearchQueryDataFromAisekaiResponse(AisekaiIntegration.Models.SearchResponse response)
        {
            var characters = new List<Models.Database.Character>();

            foreach (var c in response.Characters)
            {
                try
                {
                    var cc = CharacterFromAisekaiCharacterInfo(c);
                    characters.Add(cc);
                }
                catch { continue; }
            }

            return new(characters.ToList(), response.OriginalQuery, IntegrationType.Aisekai) { ErrorReason = response.ErrorReason };
        }

        internal static SearchQueryData SearchQueryDataFromChubResponse(IntegrationType type, ChubSearchResponse? response)
        {
            var characters = new List<Models.Database.Character>();
            if (response is null)
                return new(characters, string.Empty, type);

            foreach (var c in response.Characters)
            {
                var cc = CharacterFromChubCharacterInfo(c);
                if (cc is not null) characters.Add(cc);
            }

            return new(characters, response.OriginalQuery, type) { ErrorReason = response.ErrorReason };
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

        internal static Models.Database.Character CharacterFromAisekaiCharacterInfo(AisekaiIntegration.Models.Character aisekaiCharacter)
        {
            return new()
            {
                Id = aisekaiCharacter.Id,
                Name = aisekaiCharacter.Name,
                Title = $"Tags: `{(aisekaiCharacter.Tags is null ? "none" : string.Join(", ", aisekaiCharacter.Tags))}`\nNSFW: `{aisekaiCharacter.NSFW}`",
                Greeting = string.Empty,
                Description = aisekaiCharacter.Description,
                AuthorName = aisekaiCharacter.Author,
                AvatarUrl = aisekaiCharacter.AvatarUrl,
                ImageGenEnabled = false,
                Interactions = aisekaiCharacter.ChatCount,
                Stars = aisekaiCharacter.LikeCount
            };
        }

        internal static Models.Database.Character? CharacterFromChubCharacterInfo(ChubCharacter? chubCharacter)
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

        public static async Task<HordeKoboldAiResult> TryToAwaitForHordeRequestResultAsync(string? messageId, HttpClient httpClient, int attemptCount)
        {
            string url = $"https://horde.koboldai.net/api/v2/generate/text/status/{messageId}";

            try
            {
                using var response = await httpClient.GetAsync(url);
                var content = await response.Content.ReadAsJsonAsync();

                if ($"{content!.done}".ToBool())
                {
                    var generations = (JArray)content.generations;
                    string text = (generations.First() as dynamic).text;

                    return new()
                    {
                        IsSuccessful = true,
                        Message = text
                    };
                }
                else if (!$"{content.is_possible}".ToBool() || $"{content.faulted}".ToBool())
                {
                    return new()
                    {
                        IsSuccessful = false,
                        ErrorReason = "Request failed. Try again later or change the model."
                    };
                }
                else
                {
                    if (attemptCount > 20) // 2 min max
                    {
                        return new()
                        {
                            IsSuccessful = false,
                            ErrorReason = "Timed out"
                        };
                    }
                    else
                    {
                        await Task.Delay(6000);
                        return await TryToAwaitForHordeRequestResultAsync(messageId, httpClient, attemptCount + 1);
                    }
                }
            }
            catch
            {
                return new()
                {
                    IsSuccessful = false,
                    ErrorReason = "Something went wrong"
                };
            }
        }

        internal static async Task<bool> UserIsBannedCheckOnly(ulong userId)
            => (await new StorageContext().BlockedUsers.FindAsync(userId)) is not null;

        internal async Task<bool> UserIsBanned(SocketCommandContext context)
        {
            var user = context.Message.Author;
            var channel = context.Channel;

            return await CheckIfUserIsBannedAsync(user, channel, context.Client);
        }

        internal async Task<bool> UserIsBanned(SocketReaction reaction, DiscordSocketClient client)
        {
            var user = reaction.User.GetValueOrDefault();
            var channel = reaction.Channel;
            if (user is null) return true;

            return await CheckIfUserIsBannedAsync(user, channel, client);
        }

        internal async Task<bool> CheckIfUserIsBannedAsync(IUser user, ISocketMessageChannel channel, DiscordSocketClient client)
        {
            var db = new StorageContext();
            
            var blockedUser = await db.BlockedUsers.FindAsync(user.Id);
            if (blockedUser is not null) return true;

            int currentMinuteOfDay = DateTime.UtcNow.Minute + DateTime.UtcNow.Hour * 60;

            // Start watching for user
            if (!_watchDog.ContainsKey(user.Id))
                _watchDog.Add(user.Id, new(-1, 0)); // user id : (current minute : count)

            // Drop + update user stats if he replies in another minute
            if (_watchDog[user.Id].Key != currentMinuteOfDay)
                _watchDog[user.Id] = new(currentMinuteOfDay, 0);

            // Update interactions count within current minute
            _watchDog[user.Id] = new(_watchDog[user.Id].Key, _watchDog[user.Id].Value + 1);

            int rateLimit = int.Parse(ConfigFile.RateLimit.Value!);

            if (_watchDog[user.Id].Value == rateLimit - 2)
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} {MentionUtils.MentionUser(user.Id)} Warning! If you proceed to call the bot so fast, you'll be blocked from using it.".ToInlineEmbed(Color.Orange));
                return false;
            }

            if (_watchDog[user.Id].Value <= rateLimit)
            {
                return false;
            }

            await db.BlockedUsers.AddAsync(new() { Id = user.Id, From = DateTime.UtcNow, Hours = 24 });
            await TryToSaveDbChangesAsync(db);

            _watchDog.Remove(user.Id);

            var textChannel = await client.GetChannelAsync(channel.Id) as SocketTextChannel;
            await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} {user.Mention}, you were calling the characters way too fast and have exceeded the rate limit.\nYou will not be able to use the bot in next 24 hours.".ToInlineEmbed(Color.Red));

            TryToReportInLogsChannel(client, title: $":eyes: Notification",
                                             desc: $"Server: **{textChannel?.Guild.Name} ({textChannel?.Guild.Id})** owned by **{textChannel?.Guild.Owner.Username} ({textChannel?.Guild.OwnerId})**\n" +
                                                   $"User **{user.Username} ({user.Id})** hit the rate limit and was blocked",
                                             content: null,
                                             color: Color.LightOrange,
                                             error: false);

            return true;
        }

        internal async Task<string?> UpdateGuildAisekaiAuthTokenAsync(ulong guildId, string refreshToken)
        {
            var newToken = await AisekaiClient.RefreshUserTokenAsync(refreshToken);

            if (newToken is not null)
            {
                var db = new StorageContext();
                var guild = await FindOrStartTrackingGuildAsync(guildId, db);
                guild.GuildAisekaiAuthToken = newToken;
                await TryToSaveDbChangesAsync(db);
            }

            return newToken;
        }

        public void WatchDogClear()
        {
            _watchDog.Clear();
        }

        internal static Embed SuccessEmbed(string message = "Success", string? imageUrl = null)
            => $"{OK_SIGN_DISCORD} {message}".ToInlineEmbed(Color.Green, imageUrl: imageUrl);
    }
}

