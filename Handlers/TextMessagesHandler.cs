using Discord;
using Discord.Commands;
using Discord.WebSocket;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using CharacterEngineDiscord.Models.Database;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Interfaces;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp.Helpers;


namespace CharacterEngineDiscord.Handlers
{
    internal class TextMessagesHandler(IDiscordClient client, IIntegrationsService integrations)
    {
        public Task HandleMessage(SocketMessage message)
        {
            Task.Run(async () => {
                try { await HandleMessageAsync(message); }
                catch (Exception e) { HandleTextMessageException(message, e); }
            });

            return Task.CompletedTask;
        }

        private async Task HandleMessageAsync(SocketMessage sm)
        {
            var userMessage = sm as SocketUserMessage;
            bool invalidInput = userMessage is null || Equals(userMessage.Author.Id, client.CurrentUser.Id) || userMessage.Content.StartsWith("~ignore");
            if (invalidInput) return;

            var context = new SocketCommandContext(client as DiscordSocketClient, userMessage);
            if (context.Channel is not IGuildChannel guildChannel) return;

            // It throws NullReferenceException sometimes for some reason
            try { if (context.Guild.CurrentUser.GetPermissions(guildChannel).SendMessages is false) return; }
            catch { return; }

            ulong channelId;
            bool isThread;
            if (guildChannel is IThreadChannel tc)
            {
                channelId = tc.CategoryId ?? 0; // parent channel of the thread
                isThread = true;
            }
            else
            {
                channelId = guildChannel.Id;
                isThread = false;
            }

            var calledCharacters = await DetermineCalledCharacters(userMessage!, channelId);
            if (calledCharacters.Count == 0 || await integrations.UserIsBanned(context)) return;
            if (integrations.GuildIsAbusive(context.Guild.Id))
                await Task.Delay(10000);
            
            foreach (var characterWebhook in calledCharacters)
            {
                int delay = characterWebhook.ResponseDelay;
                if (context.User.IsWebhook || context.User.IsBot)
                {
                    delay = Math.Max(10, delay);
                }

                await Task.Delay(delay * 1000);
                await TryToCallCharacterAsync(characterWebhook.Id, context, isThread);
            }
        }

        private async Task TryToCallCharacterAsync(ulong characterWebhookId, SocketCommandContext context, bool isThread)
        {
            await using var db = new StorageContext();

             //////////////////
            /// Prevalidations
            var characterWebhook = await db.CharacterWebhooks.FindAsync(characterWebhookId);

            if (characterWebhook is null) return;
            if (characterWebhook.IntegrationType is IntegrationType.Empty) return;

            if ((context.User.IsWebhook || context.User.IsBot) && characterWebhook.SkipNextBotMessage)
            {
                characterWebhook.SkipNextBotMessage = false;
                await TryToSaveDbChangesAsync(db);
                return;
            }

            // Can be `true` when two characters speak to each other, and one of them suddenly sends error message.
            // In this case, bot will let one error message pass as a normal message (so 2nd character could see it),
            // but it will brace to ignore the second message if it will return an error again.
            bool isErrorMessage = context.Message.Embeds.Count != 0 && string.IsNullOrEmpty(context.Message.Content);

            // If this one is true, it means that the last message was containing some error...
            if (characterWebhook.SkipNextErrorMessage)
            {   // ...if current one is failure too, just stop convo...
                if (isErrorMessage) return;
                // ...if current one is ok, just proceed as normal
                characterWebhook.SkipNextErrorMessage = false;
            }

            string userName;
            if (context.User is SocketGuildUser guildUser)
                userName = guildUser.GetBestName();
            else
                userName = context.User.Username;

            bool canNotProceed = !await EnsureCharacterCanBeCalledAsync(characterWebhook, context.Channel);
            if (canNotProceed)
            {
                await TryToSaveDbChangesAsync(db);
                return;
            }

             ////////////////////
            /// Reformat message
            string text;
            if (isErrorMessage)
            {
                text = $"{context.Message.Embeds.First().Description}";
                characterWebhook.SkipNextErrorMessage = true; // prepare to stop characters convo if the error will repeat again
            }
            else
            {
                text = context.Message.Content ?? string.Empty;
                if (context.Message.Attachments.FirstOrDefault() is Attachment file)
                    text += $"((sends file \"{file.Filename}\"))";
            }

            // Replace @mentions with normal names
            var userMentions = MentionRegex().Matches(text).ToArray();
            foreach (var mention in userMentions)
            {   try
                {   var userId = MentionUtils.ParseUser(mention.Value);
                    if (await context.Channel.GetUserAsync(userId) is IGuildUser user)
                        text = text.Replace(mention.ToString(), (user.IsBot || user.IsWebhook) ? user.Username : user.GetBestName());
                }
                catch { continue; }
            }

            // Bring to format template
            var formatTemplate = characterWebhook.PersonalMessagesFormat ?? characterWebhook.Channel.Guild.GuildMessagesFormat ?? ConfigFile.DefaultMessagesFormat.Value!;
            text = formatTemplate.Replace("{{user}}", $"{userName}")
                                 .Replace("{{msg}}", $"{text.RemovePrefix(characterWebhook.CallPrefix)}")
                                 .Replace("\\n", "\n");

            text = await text.AddRefQuoteAsync(context.Message.ReferencedMessage);

             //////////////////////////
            /// Get character response
            CharacterResponse characterResponse = null!;
            if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                characterResponse = await CallOpenAiCharacterAsync(characterWebhook, text);
            else if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
                characterResponse = await CallCaiCharacterAsync(characterWebhook, text);
            else if (characterWebhook.IntegrationType is IntegrationType.Aisekai)
                characterResponse = await CallAisekaiCharacterAsync(characterWebhook, text);
            else if (characterWebhook.IntegrationType is IntegrationType.KoboldAI)
                characterResponse = await CallKoboldAiCharacterAsync(characterWebhook, text);
            else if (characterWebhook.IntegrationType is IntegrationType.HordeKoboldAI)
                characterResponse = await CallHordeKoboldAiCharacterAsync(characterWebhook, text);

            var messageId = TryToSendCharacterMessageAsync(characterWebhook, characterResponse, context, userName, isThread);
            TryToRemoveButtons(characterWebhook.LastCharacterDiscordMsgId, context.Channel);

            characterWebhook.CurrentSwipeIndex = 0;
            characterWebhook.LastCharacterMsgId = characterResponse.CharacterMessageId;
            characterWebhook.LastUserMsgId = characterResponse.UserMessageId;
            characterWebhook.LastDiscordUserCallerId = context.User.Id;
            characterWebhook.LastCallTime = DateTime.UtcNow;
            characterWebhook.MessagesSent++;
            characterWebhook.LastCharacterDiscordMsgId = await messageId;
            characterWebhook.Channel.Guild.MessagesSent++;

            await TryToSaveDbChangesAsync(db);
        }


        /// <returns>Message ID or 0 if sending failed</returns>
        private async Task<ulong> TryToSendCharacterMessageAsync(CharacterWebhook characterWebhook, Models.Common.CharacterResponse characterResponse, SocketCommandContext context, string userName, bool isThread)
        {
            integrations.Conversations.TryAdd(characterWebhook.Id, new(new(), DateTime.UtcNow));
            var convo = integrations.Conversations[characterWebhook.Id];

            await convo.Locker.WaitAsync();
            try
            {   // Forget all choises from the last message and remember a new one
                convo.AvailableMessages.Clear();
                convo.AvailableMessages.Add(new()
                {
                    Text = characterResponse.Text,
                    MessageId = characterResponse.CharacterMessageId,
                    ImageUrl = characterResponse.ImageRelPath,
                    TokensUsed = characterResponse.TokensUsed
                });
                convo.LastUpdated = DateTime.UtcNow;
            }
            finally
            {
                convo.Locker.Release();
            }

            var webhookClient = integrations.GetWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            if (webhookClient is null)
            {
                await context.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to send character message: `Channel webhook was not found`".ToInlineEmbed(Color.Red));
                return 0;
            }

            // Reformat message
            List<Embed>? embeds = new();
            string characterMsgText;
            bool errorMessage;

            if (characterResponse.IsFailure)
            {
                characterMsgText = string.Empty;
                errorMessage = true;

                embeds.Add($"\n{characterResponse.Text}".ToInlineEmbed(Color.Red, bold: false));
            }
            else
            {
                characterMsgText = $"{(context.User.IsWebhook ? $"**{context.User.Username}**," : context.User.Mention)} {characterResponse.Text.Replace("{{user}}", $"**{userName}**")}";
                errorMessage = false;

                // Cut if too long
                if (characterMsgText.Length > 2000)
                    characterMsgText = characterMsgText[0..1994] + "[...]";

                // Add quote
                string? quote = context.Message.Content;
                if (characterWebhook.ReferencesEnabled && !string.IsNullOrWhiteSpace(quote))
                {   // Replace @mentions with normal names
                    var userMentions = MentionRegex().Matches(quote).ToArray();
                    foreach (var mention in userMentions) {
                        try {
                            var userId = MentionUtils.ParseUser(mention.Value);
                            if (await context.Channel.GetUserAsync(userId) is not IGuildUser user) continue;
                            else quote = quote.Replace(mention.ToString(), (user.IsBot || user.IsWebhook) ? user.Username : user.GetBestName());
                        }
                        catch { continue; }
                    }

                    int l = Math.Min(quote.Length, 50);
                    embeds.Add(new EmbedBuilder().WithFooter($"> {quote[0..l]}{(l == 50 ? "..." : "")}").Build());
                }

                if (characterResponse.ImageRelPath is not null)
                {
                    bool canGetImage = await CheckIfImageIsAvailableAsync(characterResponse.ImageRelPath, integrations.ImagesHttpClient);
                    if (canGetImage) embeds.Add(new EmbedBuilder().WithImageUrl(characterResponse.ImageRelPath).Build());
                }
            }

            // Sending message
            try
            {
                ulong messageId;
                if (isThread)
                    messageId = await webhookClient.SendMessageAsync(characterMsgText, embeds: embeds, threadId: context.Channel.Id);
                else
                    messageId = await webhookClient.SendMessageAsync(characterMsgText, embeds: embeds);

                integrations.MessagesSent++;

                if (!errorMessage)
                    await TryToAddButtonsAsync(characterWebhook, context.Channel, messageId, responseToBot: (context.User.IsWebhook || context.User.IsBot));

                return messageId;
            }
            catch (Exception e)
            {
                try { await context.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to send character message: `{e.Message}`".ToInlineEmbed(Color.Red)); }
                catch { }
                return 0;
            }
        }


        // Calls

        private async Task<Models.Common.CharacterResponse> CallCaiCharacterAsync(CharacterWebhook cw, string text)
        {
            string caiToken = cw.Channel.Guild.GuildCaiUserToken ?? string.Empty;
            bool plusMode = cw.Channel.Guild.GuildCaiPlusMode ?? false;
            string charId = cw.Character.Id ?? string.Empty;
            string tgt = cw.Character.Tgt ?? string.Empty;
            string historyId = cw.ActiveHistoryID ?? string.Empty;

            while (integrations.CaiReloading)
                await Task.Delay(5000);

            var id = Guid.NewGuid();
            integrations.RunningCaiTasks.Add(id);
            try
            {
                var response = await integrations.CaiClient!.CallCharacterAsync(charId, tgt, historyId, text,
                    primaryMsgUuId: cw.LastCharacterMsgId, authToken: caiToken, plusMode: plusMode).WithTimeout(60000);

                string message;
                bool success;

                if (response.IsSuccessful)
                {
                    message = response.Response!.Text;
                    success = true;
                }
                else
                {
                    message =
                        $"{WARN_SIGN_DISCORD} **Failed to fetch character response:** ```\n{response.ErrorReason}\n```";
                    success = false;
                }

                return new()
                {
                    Text = message,
                    IsSuccessful = success,
                    CharacterMessageId = response.Response?.UuId,
                    ImageRelPath = response.Response?.ImageRelPath,
                    UserMessageId = response.LastUserMsgUuId
                };
            }
            finally { integrations.RunningCaiTasks.Remove(id); }
        }

        private async Task<CharacterResponse> CallAisekaiCharacterAsync(CharacterWebhook characterWebhook, string text, string? authToken = null)
        {
            authToken ??= characterWebhook.Channel.Guild.GuildAisekaiAuthToken!;
            
            integrations.Conversations.TryGetValue(characterWebhook.Id, out var convo);
            
            if (convo is not null && convo.AvailableMessages.Count > 1) // there was a swipe
            {   // Try to edit character message
                var editResult = await EditLastAisekaiCharacterMessageAsync(characterWebhook, authToken, convo);
                if (editResult.Key is false)
                {
                    return new()
                    {
                        Text = editResult.Value!,
                        IsSuccessful = false
                    };
                }
            }

            return await SendAisekaiCharacterMessageAsync(characterWebhook, text, authToken);
        }

        private async Task<CharacterResponse> SendAisekaiCharacterMessageAsync(CharacterWebhook characterWebhook, string text, string authToken)
        {
            string message;
            string? lastMessageId = null;

            var response = await integrations.AisekaiClient.PostChatMessageAsync(authToken, characterWebhook.ActiveHistoryID!, text);

            if (response.IsSuccessful)
            {
                message = response.CharacterResponse!.Value.Content;
                lastMessageId = response.CharacterResponse!.Value.LastMessageId;
            }
            else if (response.Code == 401)
            {
                string? newAuthToken = await integrations.UpdateGuildAisekaiAuthTokenAsync(characterWebhook.Channel.Guild.Id, characterWebhook.Channel.Guild.GuildAisekaiRefreshToken ?? "");
                if (newAuthToken is null)
                    message = $"{WARN_SIGN_DISCORD} Failed to authorize Aisekai account`";
                else
                    return await SendAisekaiCharacterMessageAsync(characterWebhook, text, newAuthToken);
            }
            else
            {
                message = $"{WARN_SIGN_DISCORD} Failed to create new chat with a character: `{response.ErrorReason}`";
            }

            return new()
            {
                Text = message,
                CharacterMessageId = lastMessageId,
                IsSuccessful = response.IsSuccessful
            };
        }

        private async Task<KeyValuePair<bool, string?>> EditLastAisekaiCharacterMessageAsync(CharacterWebhook characterWebhook, string authToken, LastCharacterCall convo)
        {
            var response = await integrations.AisekaiClient.PatchEditMessageAsync(authToken, characterWebhook.ActiveHistoryID!, convo.AvailableMessages[0].MessageId!, convo.AvailableMessages[characterWebhook.CurrentSwipeIndex].Text!);

            if (response.IsSuccessful)
            {
                return new(true, null);
            }
            if (response.Code == 401)
            {
                string? newAuthToken = await integrations.UpdateGuildAisekaiAuthTokenAsync(characterWebhook.Channel.Guild.Id, characterWebhook.Channel.Guild.GuildAisekaiRefreshToken ?? "");
                if (newAuthToken is null)
                    return new(false, $"{WARN_SIGN_DISCORD} Failed to authorize Aisekai account`");
                else
                    return await EditLastAisekaiCharacterMessageAsync(characterWebhook, newAuthToken, convo);
            }
            else
            {
                return new(false, $"{WARN_SIGN_DISCORD} Failed to create new chat with a character: `{response.ErrorReason}`");
            }
        }

        private async Task<CharacterResponse> CallOpenAiCharacterAsync(CharacterWebhook cw, string text)
        {            
            cw.StoredHistoryMessages.Add(new() { Role = "user", Content = text, CharacterWebhookId = cw.Id }); // remember user message (will be included in payload)
            var openAiRequestParams = BuildChatOpenAiRequestPayload(cw);
            if (openAiRequestParams.Messages.Count < 2)
            {
                return new()
                {
                    Text = $"{WARN_SIGN_DISCORD} Failed to fetch character response: `Your message couldn't fit in the max token limit`",
                    TokensUsed = 0,
                    IsSuccessful = false
                };
            }

            string message;
            bool success;
            int tokens = 0;
            string? charMsgId = null;

            var openAiResponse = await SendOpenAiRequestAsync(openAiRequestParams, integrations.CommonHttpClient);

            if (openAiResponse is null || openAiResponse.IsFailure || openAiResponse.Message.IsEmpty())
            {
                string desc = (openAiResponse?.ErrorReason is null || openAiResponse.ErrorReason.Contains("IP")) ? "Something went wrong" : openAiResponse.ErrorReason;
                message = $"{WARN_SIGN_DISCORD} **Failed to fetch character response:** ```\n{desc}\n```";
                success = false;
            }
            else
            {
                // Remember character message
                cw.StoredHistoryMessages.Add(new() { Role = "assistant", Content = openAiResponse.Message!, CharacterWebhookId = cw.Id });
                cw.LastRequestTokensUsage = openAiResponse.Usage ?? 0;

                // Clear old messages, 80-100 is a good balance between response speed and needed context size, also it's usually pretty close to the GPT-3.5 token limit
                if (cw.StoredHistoryMessages.Count > 100)
                    cw.StoredHistoryMessages.RemoveRange(0, 20);

                message = openAiResponse.Message!;
                success = true;
                tokens = openAiResponse.Usage ?? 0;
                charMsgId = openAiResponse.MessageId;
            }

            return new()
            {
                Text = message,
                TokensUsed = tokens,
                CharacterMessageId = charMsgId,
                IsSuccessful = success
            };
        }

        private async Task<Models.Common.CharacterResponse> CallKoboldAiCharacterAsync(CharacterWebhook cw, string text)
        {
            cw.StoredHistoryMessages.Add(new() { Role = $"\n<USER>\n", Content = text, CharacterWebhookId = cw.Id }); // remember user message (will be included in payload)
            var koboldAiRequestParams = BuildKoboldAiRequestPayload(cw);

            if (koboldAiRequestParams.Messages.Count < 2)
            {
                return new()
                {
                    Text = $"{WARN_SIGN_DISCORD} Failed to fetch character response: `Your message couldn't fit in the max token limit`",
                    IsSuccessful = false
                };
            }

            string message;
            bool success;

            var kobkoldAiResponse = await SendKoboldAiRequestAsync(cw.Character.Name, koboldAiRequestParams, integrations.CommonHttpClient, continueRequest: false);

            if (kobkoldAiResponse is null || kobkoldAiResponse.IsFailure || kobkoldAiResponse.Message.IsEmpty())
            {
                string desc = kobkoldAiResponse?.ErrorReason ?? "Something went wrong";
                message = $"{WARN_SIGN_DISCORD} **Failed to fetch character response:** ```\n{desc}\n```";
                success = false;
            }
            else
            {   // Remember character message
                cw.StoredHistoryMessages.Add(new() { Role = $"\n<{cw.Character.Name}>\n", Content = kobkoldAiResponse.Message!, CharacterWebhookId = cw.Id });

                if (cw.StoredHistoryMessages.Count > 100)
                    cw.StoredHistoryMessages.RemoveRange(0, 20);

                message = kobkoldAiResponse.Message!;
                success = true;
            }

            return new()
            {
                Text = message,
                IsSuccessful = success
            };
        }

        private async Task<Models.Common.CharacterResponse> CallHordeKoboldAiCharacterAsync(CharacterWebhook cw, string text)
        {
            cw.StoredHistoryMessages.Add(new() { Role = $"\n<USER>\n", Content = text, CharacterWebhookId = cw.Id }); // remember user message (will be included in payload)
            var hordeRequestParams = BuildHordeKoboldAiRequestPayload(cw);

            if (hordeRequestParams.KoboldAiSettings.Messages.Count < 2)
            {
                return new()
                {
                    Text = $"{WARN_SIGN_DISCORD} Failed to fetch character response: `Your message couldn't fit in the max token limit`",
                    IsSuccessful = false
                };
            }

            string message;
            bool success;

            var hordeResponse = await SendHordeKoboldAiRequestAsync(cw.Character.Name, hordeRequestParams, integrations.CommonHttpClient, continueRequest: false);

            if (hordeResponse is null || hordeResponse.IsFailure || hordeResponse.Id.IsEmpty())
            {
                string desc = hordeResponse?.ErrorReason ?? "Something went wrong";
                message = $"{WARN_SIGN_DISCORD} **Failed to fetch character response:** ```\n{desc}\n```";
                success = false;
            }
            else
            {   // Remember character message
                var hordeResult = await TryToAwaitForHordeRequestResultAsync(hordeResponse.Id, integrations.CommonHttpClient, 0);
                if (hordeResult.IsSuccessful)
                {
                    cw.StoredHistoryMessages.Add(new() { Role = $"\n<{cw.Character.Name}>\n", Content = hordeResult.Message!, CharacterWebhookId = cw.Id });

                    if (cw.StoredHistoryMessages.Count > 100)
                        cw.StoredHistoryMessages.RemoveRange(0, 20);

                    message = hordeResult.Message!;
                    success = true;
                }
                else
                {
                    message = hordeResult.ErrorReason!;
                    success = false;
                }
            }

            return new()
            {
                Text = message,
                IsSuccessful = success
            };
        }

        // Ensure

        private static async Task<bool> EnsureCharacterCanBeCalledAsync(CharacterWebhook characterWebhook, ISocketMessageChannel channel)
        {
            bool result;
            if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
                result = await EnsureCaiCharacterCanBeCalledAsync(characterWebhook, channel);
            else if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                result = await EnsureOpenAiCharacterCanBeCalledAsync(characterWebhook, channel);
            else if (characterWebhook.IntegrationType is IntegrationType.Aisekai)
                result = await EnsureAisekaiCharacterCanBeCalledAsync(characterWebhook, channel);
            else if (characterWebhook.IntegrationType is IntegrationType.KoboldAI)
                result = await EnsureKoboldAiCharacterCanBeCalledAsync(characterWebhook, channel);
            else if (characterWebhook.IntegrationType is IntegrationType.HordeKoboldAI)
                result = await EnsureHordeKoboldAiCharacterCanBeCalledAsync(characterWebhook, channel);
            else
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} You have to set a backend API for this integration. Use `/update set-api` command.".ToInlineEmbed(Color.Orange));
                result = false;
            }

            return result;
        }

        private static async Task<bool> EnsureCaiCharacterCanBeCalledAsync(CharacterWebhook characterWebhook, ISocketMessageChannel channel)
        {
            if (!ConfigFile.CaiEnabled.Value.ToBool())
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} CharacterAI integration is not available".ToInlineEmbed(Color.Red));
                return false;
            }

            var caiToken = characterWebhook.Channel.Guild.GuildCaiUserToken;

            if (string.IsNullOrWhiteSpace(caiToken))
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} You have to specify an CharacterAI auth token for your server first!".ToInlineEmbed(Color.Red));
                return false;
            }

            return true;
        }

        private static async Task<bool> EnsureAisekaiCharacterCanBeCalledAsync(CharacterWebhook characterWebhook, ISocketMessageChannel channel)
        {
            var authToken = characterWebhook.Channel.Guild.GuildAisekaiAuthToken;

            if (string.IsNullOrWhiteSpace(authToken))
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} You have to specify an Aisekai account for your server first!".ToInlineEmbed(Color.Red));
                return false;
            }

            return true;
        }

        private static async Task<bool> EnsureOpenAiCharacterCanBeCalledAsync(CharacterWebhook cw, ISocketMessageChannel channel)
        {
            string? openAiToken = cw.PersonalApiToken ?? cw.Channel.Guild.GuildOpenAiApiToken;

            if (string.IsNullOrWhiteSpace(openAiToken))
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} You have to specify an OpenAI API token for your server first!".ToInlineEmbed(Color.Red));
                return false;
            }

            return true;
        }

        private static async Task<bool> EnsureKoboldAiCharacterCanBeCalledAsync(CharacterWebhook cw, ISocketMessageChannel channel)
        {
            string? koboldAiApiEndpoint = cw.PersonalApiEndpoint ?? cw.Channel.Guild.GuildKoboldAiApiEndpoint;

            if (string.IsNullOrWhiteSpace(koboldAiApiEndpoint))
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} You have to specify an KoboldAI API endpoint for your server first!".ToInlineEmbed(Color.Red));
                return false;
            }

            return true;
        }

        private static async Task<bool> EnsureHordeKoboldAiCharacterCanBeCalledAsync(CharacterWebhook cw, ISocketMessageChannel channel)
        {
            string? hordeApiToken = cw.PersonalApiToken ?? cw.Channel.Guild.GuildHordeApiToken;

            if (string.IsNullOrWhiteSpace(hordeApiToken))
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} You have to specify an KoboldAI API endpoint for your server first!".ToInlineEmbed(Color.Red));
                return false;
            }

            return true;
        }


        // Other

        private static readonly Random random = new();
        private static async Task<List<CharacterWebhook>> DetermineCalledCharacters(SocketUserMessage userMessage, ulong channelId)
        {
            List<CharacterWebhook> calledCharacters = new();

            await using var db = new StorageContext();
            var channel = await db.Channels.FindAsync(channelId);
            if (channel is null)
                return calledCharacters;

            var text = userMessage.Content.Trim();
            var rm = userMessage.ReferencedMessage;
            var withRefMessage = rm is not null && rm.Author.IsWebhook;
            var chance = (float)(random.Next(99) + 0.001 + random.NextDouble());

            // Try to find one certain character that was called by a prefix
            var cw = channel.CharacterWebhooks.FirstOrDefault(cwh => text.StartsWith(cwh.CallPrefix));

            if (cw is not null)
            {
                calledCharacters.Add(cw);
            }
            else if (withRefMessage) // or find some other that was called by a reply
            {
                cw = channel.CharacterWebhooks.FirstOrDefault(cwh => cwh.Id == rm!.Author.Id);
                if (cw is not null) calledCharacters.Add(cw);
            }

            // Add characters who hunt the user            
            var hunters = channel.CharacterWebhooks.Where(w => w.HuntedUsers.Any(h => h.UserId == userMessage.Author.Id && h.Chance > chance)).ToList();
            if (hunters.Count != 0)
            {
                foreach (var h in hunters.Where(h => !calledCharacters.Contains(h)))
                    calledCharacters.Add(h);
            }

            // Add some random character (only one) by channel's random reply chance
            if (channel.RandomReplyChance > chance)
            {
                var characters = channel.CharacterWebhooks.Where(w => w.Id != userMessage.Author.Id).ToList();
                if (characters.Count > 0)
                {
                    var someRandomCharacter = characters[random.Next(characters.Count)];
                    if (!calledCharacters.Contains(someRandomCharacter))
                        calledCharacters.Add(someRandomCharacter);
                }
            }

            // Add certain random characters by their personal random reply chance
            var randomCharacters = channel.CharacterWebhooks.Where(w => w.Id != userMessage.Author.Id && w.ReplyChance > chance).ToList();
            if (randomCharacters.Count > 0)
            {
                foreach (var rc in randomCharacters.Where(rc => !calledCharacters.Contains(rc)))
                    calledCharacters.Add(rc);
            }

            return calledCharacters;
        }
        
        private static async Task TryToAddButtonsAsync(CharacterWebhook characterWebhook, ISocketMessageChannel channel, ulong messageId, bool responseToBot)
        {   try
            {
                var message = await channel.GetMessageAsync(messageId);

                if (!responseToBot)
                {
                    if (characterWebhook.SwipesEnabled)
                    {
                        await message.AddReactionAsync(ARROW_LEFT);
                        await message.AddReactionAsync(ARROW_RIGHT);
                    }
                    if (characterWebhook.CrutchEnabled)
                    {
                        await message.AddReactionAsync(CRUTCH_BTN);
                    }
                }
                else if (characterWebhook.StopBtnEnabled)
                {
                    await message.AddReactionAsync(STOP_BTN);
                }
            }
            catch
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to add reaction-buttons to the character message.\nMake sure that bot has permission to add reactions in this channel, or disable this feature with `/update toggle-swipes enable:false` command.".ToInlineEmbed(Color.Red));
            }
        }

        private void TryToRemoveButtons(ulong oldMessageId, ISocketMessageChannel channel)
        {
            Task.Run(async () =>
            {
                if (oldMessageId == 0) return;

                var oldMessage = await channel.GetMessageAsync(oldMessageId);
                if (oldMessage is null) return;

                var btns = new Emoji[] { ARROW_LEFT, ARROW_RIGHT, CRUTCH_BTN, STOP_BTN };
                await Parallel.ForEachAsync(btns, async (btn, ct)
                    => await oldMessage.RemoveReactionAsync(btn, client.CurrentUser));
            });
        }

        private void HandleTextMessageException(SocketMessage message, Exception e)
        {
            LogException(new[] { e });

            if (e.Message.Contains("Missing Permissions")) return;

            var channel = message.Channel as SocketGuildChannel;
            var guild = channel?.Guild;
            TryToReportInLogsChannel(client, title: "Message Exception",
                                              desc: $"Guild: `{guild?.Name} ({guild?.Id})`\n" +
                                                    $"Owner: `{guild?.Owner.GetBestName()} ({guild?.Owner.Username})`\n" +
                                                    $"Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                    $"User: `{message.Author.Username}" + (message.Author.IsWebhook ? " (webhook)`" : message.Author.IsBot ? " (bot)`" : "`") +
                                                    $"\nMessage: `{message.Content[0..Math.Min(message.Content.Length, 1000)]}`",
                                              content: e.ToString(),
                                              color: Color.Red,
                                              error: true);
        }
    }
}
