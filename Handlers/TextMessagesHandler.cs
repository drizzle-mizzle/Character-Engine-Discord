using Discord;
using Discord.Webhook;
using Discord.Commands;
using Discord.WebSocket;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using CharacterEngineDiscord.Models.Database;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngineDiscord.Handlers
{
    internal partial class TextMessagesHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationsService _integration;

        public TextMessagesHandler(IServiceProvider services)
        {
            _services = services;
            _integration = _services.GetRequiredService<IntegrationsService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            _client.MessageReceived += (message) =>
            {
                Task.Run(async () => {
                    try { await HandleMessageAsync(message); }
                    catch (Exception e) { await HandleTextMessageException(message, e); }
                });
                return Task.CompletedTask;
            };
        }

        private async Task HandleMessageAsync(SocketMessage sm)
        {
            if (sm is not SocketUserMessage userMessage) return;
            if (userMessage.Author.Id == _client.CurrentUser.Id) return;
            if (string.IsNullOrWhiteSpace(userMessage.Content)) return;
            if (userMessage.Content.Trim().StartsWith("~ignore")) return;

            var context = new SocketCommandContext(_client, userMessage);

            if (context.Guild is null) return;
            if (context.Channel is not IGuildChannel currentChannel) return;
            if (context.User is not IGuildUser guildUser) return;
            if (context.Guild.CurrentUser.GetPermissions(currentChannel).SendMessages is false) return;

            var calledCharacters = await DetermineCalledCharacterWebhook(userMessage, currentChannel.Id);

            if (calledCharacters.Count == 0) return;
            if (await _integration.UserIsBanned(context)) return;

            foreach (var characterWebhook in calledCharacters)
            {
                int delay = characterWebhook.ResponseDelay;

                if (guildUser.IsWebhook || guildUser.IsBot)
                    delay = Math.Max(10, delay);

                await Task.Delay(delay * 1000);
                await TryToCallCharacterAsync(characterWebhook.Id, userMessage);
            }
        }

        private async Task TryToCallCharacterAsync(ulong characterWebhookId, SocketUserMessage userMessage)
        {
            var db = new StorageContext();
            var characterWebhook = await db.CharacterWebhooks.FindAsync(characterWebhookId);
            if (characterWebhook is null) return;

            string userName;
            if (userMessage.Author is SocketGuildUser guildUser)
                userName = guildUser.GetBestName();
            else if (userMessage.Author.IsWebhook)
                userName = userMessage.Author.Username;
            else return;

            // Ensure character can be called
            bool canProceed = false;
            if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                canProceed = await CanCallOpenAiCharacter(characterWebhook, userMessage);
            else if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
                canProceed = await CanCallCaiCharacter(characterWebhook, userMessage, _integration);
            else
                await userMessage.ReplyAsync(embed: $"{WARN_SIGN_DISCORD} You have to set backend API for this integration. Use `/update set-api` command.".ToInlineEmbed(Color.Orange));

            if (!canProceed) return;

            // Reformat message
            string text = userMessage.Content ?? "";
            if (text.StartsWith("<")) text = MentionRegex().Replace(text, "", 1);

            var format = characterWebhook.MessagesFormat ?? characterWebhook.Channel.Guild.GuildMessagesFormat ?? ConfigFile.DefaultMessagesFormat.Value!;
            text = format.Replace("{{user}}", $"{userName}")
                         .Replace("{{msg}}", $"{text.RemovePrefix(characterWebhook.CallPrefix)}")
                         .Replace("\\n", "\n");

            if (text.Contains("{{ref_msg_text}}"))
            {
                int start = text.IndexOf("{{ref_msg_begin}}");
                int end = text.IndexOf("{{ref_msg_end}}") + "{{ref_msg_end}}".Length;

                if (string.IsNullOrWhiteSpace(userMessage.ReferencedMessage?.Content))
                    text = text.Remove(start, end - start).Trim();
                else
                {
                    string refContent = userMessage.ReferencedMessage.Content;

                    if (refContent.StartsWith("<")) refContent = MentionRegex().Replace(refContent, "", 1);

                    string refName = userMessage.ReferencedMessage.Author is SocketGuildUser refGuildUser ? (refGuildUser.GetBestName()) : userMessage.Author.Username;
                    int refL = Math.Min(refContent.Length, 200);

                    text = text.Replace("{{ref_msg_text}}", refContent[0..refL] + (refL == 200 ? "..." : "")).Replace("{{ref_msg_user}}", refName).Replace("{{ref_msg_begin}}", "").Replace("{{ref_msg_end}}", "");
                }
            }

            // Get character response
            CharacterResponse? characterResponse = (characterWebhook.IntegrationType is IntegrationType.OpenAI) ?
                                                        await CallOpenAiCharacterAsync(characterWebhookId, userMessage, text) :
                                                   (characterWebhook.IntegrationType is IntegrationType.CharacterAI) ?
                                                        await CallCaiCharacterAsync(characterWebhookId, userMessage, text) : null;
            if (characterResponse is null) return;

            _ = Task.Run(async () => await TryToRemoveButtonsAsync(characterWebhook.LastCharacterDiscordMsgId, userMessage));
            
            await db.Entry(characterWebhook).ReloadAsync();
            characterWebhook.CurrentSwipeIndex = 0;
            characterWebhook.LastCharacterMsgUuId = characterResponse.CharacterMessageUuid;
            characterWebhook.LastUserMsgUuId = characterResponse.UserMessageId;
            characterWebhook.LastDiscordUserCallerId = userMessage.Author.Id;
            characterWebhook.LastCallTime = DateTime.UtcNow;
            characterWebhook.LastCharacterDiscordMsgId = await TryToSendCharacterMessageAsync(characterWebhook, characterResponse, userMessage, userName);

            await db.SaveChangesAsync();
        }

        private async Task TryToRemoveButtonsAsync(ulong oldMessageId, SocketUserMessage userMessage)
        {
            if (oldMessageId != 0)
            {
                var oldMessage = await userMessage.Channel.GetMessageAsync(oldMessageId);
                if (oldMessage is not null)
                {
                    var btns = new Emoji[] { ARROW_LEFT, ARROW_RIGHT, CRUTCH_BTN };
                    await Parallel.ForEachAsync(btns, async (btn, ct)
                        => await oldMessage.RemoveReactionAsync(btn, _client.CurrentUser));
                }
            }
        }

        /// <returns>Message ID</returns>
        private async Task<ulong> TryToSendCharacterMessageAsync(CharacterWebhook characterWebhook, CharacterResponse characterResponse, SocketUserMessage userMessage, string userName)
        {
            var availResponses = _integration.AvailableCharacterResponses;
            availResponses.TryAdd(characterWebhook.Id, new());

            lock (availResponses[characterWebhook.Id])
            {
                // Forget all choises from the last message and remember a new one
                availResponses[characterWebhook.Id].Clear();
                availResponses[characterWebhook.Id].Add(new()
                {
                    Text = characterResponse.Text,
                    MessageId = characterResponse.CharacterMessageUuid,
                    ImageUrl = characterResponse.ImageRelPath,
                    TokensUsed = characterResponse.TokensUsed
                });
            }

            var webhookClient = _integration.GetWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            if (webhookClient is null)
            {
                await userMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to send character message".ToInlineEmbed(Color.Red));
                return 0;
            }

            // Reformat message
            string characterMessage = characterResponse.Text.Replace("{{user}}", $"**{userName}**");
            characterMessage = $"{(userMessage.Author.IsWebhook ? $"**{userMessage.Author.Username}**," : userMessage.Author.Mention)} {characterMessage}";

            // Cut if too long
            if (characterMessage.Length > 2000)
                characterMessage = characterMessage[0..1994] + "[...]";

            // Fill embeds
            List<Embed>? embeds = new();
            if (characterWebhook.ReferencesEnabled && userMessage.Content is not null)
            {
                int l = Math.Min(userMessage.Content.Length, 36);
                string quote = userMessage.Content;
                if (quote.StartsWith("<")) quote = MentionRegex().Replace(quote, "", 1);

                embeds.Add(new EmbedBuilder().WithFooter($"> {quote[0..l]}{(l == 36 ? "..." : "")}").Build());
            }
            if (characterResponse.ImageRelPath is not null)
            {
                bool canGetImage = await TryGetImageAsync(characterResponse.ImageRelPath, _integration.HttpClient);
                if (canGetImage) embeds.Add(new EmbedBuilder().WithImageUrl(characterResponse.ImageRelPath).Build());
            }

            // Sending message
            try
            {
                ulong messageId = await webhookClient.SendMessageAsync(characterMessage, embeds: embeds);
                bool isUserMessage = !(userMessage.Author.IsWebhook || userMessage.Author.IsBot);
                if (isUserMessage) await TryToAddButtonsAsync(characterWebhook, userMessage.Channel, messageId);

                return messageId;
            }
            catch (Exception e)
            {
                await userMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to send character message: `{e.Message}`".ToInlineEmbed(Color.Red));
                return 0;
            }
        }

        private async Task<CharacterResponse?> CallOpenAiCharacterAsync(ulong characterWebhookId, SocketUserMessage userMessage, string text)
        {
            var db = new StorageContext();
            var characterWebhook = await db.CharacterWebhooks.FindAsync(characterWebhookId);
            if (characterWebhook is null) return null;

            characterWebhook.OpenAiHistoryMessages.Add(new() { Role = "user", Content = text, CharacterWebhookId = characterWebhook.Id }); // remember user message (will be included in payload)

            var openAiRequestParams = BuildChatOpenAiRequestPayload(characterWebhook);
            if (openAiRequestParams.Messages.Count < 2)
            {
                characterWebhook.OpenAiHistoryMessages.Remove(characterWebhook.OpenAiHistoryMessages.Last());
                await userMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to fetch character response: `Your message couldn't fit in the max token limit`".ToInlineEmbed(Color.Red));
                return null;
            }

            var openAiResponse = await CallChatOpenAiAsync(openAiRequestParams, _integration.HttpClient);

            if (openAiResponse is null || openAiResponse.IsFailure || openAiResponse.Message.IsEmpty())
            {
                characterWebhook.OpenAiHistoryMessages.Remove(characterWebhook.OpenAiHistoryMessages.Last());
                await userMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to fetch character response: `{openAiResponse?.ErrorReason ?? "Something went wrong!"}`".ToInlineEmbed(Color.Red));
                return null;
            }

            // Remember character message
            characterWebhook.OpenAiHistoryMessages.Add(new() { Role = "assistant", Content = openAiResponse.Message!, CharacterWebhookId = characterWebhook.Id });
            characterWebhook.LastRequestTokensUsage = openAiResponse.Usage ?? 0;

            // Clear old messages, 40-60 is a good balance between response speed and needed context size, also it's usually pretty close to the GPT-3.5 token limit
            if (characterWebhook.OpenAiHistoryMessages.Count > 60)
                characterWebhook.OpenAiHistoryMessages.RemoveRange(0, 20);

            await db.SaveChangesAsync();

            return new()
            {
                Text = openAiResponse.Message!,
                TokensUsed = openAiResponse.Usage ?? 0,
                CharacterMessageUuid = openAiResponse.MessageId,
                IsSuccessful = true,
                UserMessageId = null, ImageRelPath = null,
            };
        }

        private async Task<CharacterResponse?> CallCaiCharacterAsync(ulong cwId, SocketUserMessage userMessage, string text)
        {
            var db = new StorageContext();
            var cw = await db.CharacterWebhooks.FindAsync(cwId);
            if (cw is null) return null;

            var caiToken = cw.Channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;
            var plusMode = cw.Channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
            var caiResponse = await _integration.CaiClient!.CallCharacterAsync(cw.Character.Id, cw.Character.Tgt!, cw.CaiActiveHistoryId!, text, primaryMsgUuId: cw.LastCharacterMsgUuId, customAuthToken: caiToken, customPlusMode: plusMode);

            if (!caiResponse.IsSuccessful)
            {
                await userMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to fetch character response: ```\n{caiResponse.ErrorReason}\n```".ToInlineEmbed(Color.Red));
                return null;
            }

            return new()
            {
                Text = caiResponse.Response!.Text,
                TokensUsed = 0,
                IsSuccessful = true,
                CharacterMessageUuid = caiResponse.Response.UuId,
                UserMessageId = caiResponse.LastUserMsgUuId,
                ImageRelPath = caiResponse.Response.ImageRelPath,
            };
        }

        private static readonly Random @Random = new();
        private static async Task<List<CharacterWebhook>> DetermineCalledCharacterWebhook(SocketUserMessage userMessage, ulong channelId)
        {
            List<CharacterWebhook> characterWebhooks = new();

            var db = new StorageContext();
            var channel = await db.Channels.FindAsync(channelId);

            if (channel is null || channel.CharacterWebhooks.Count == 0)
            {
                return characterWebhooks;
            }

            var text = userMessage.Content.Trim();
            var rm = userMessage.ReferencedMessage;
            var withRefMessage = rm is not null && rm.Author.IsWebhook;
            var chance = (float)(@Random.Next(99) + 0.001 + @Random.NextDouble());

            // Try to find one certain character that was called by a prefix
            var cw = channel.CharacterWebhooks.FirstOrDefault(w => text.StartsWith(w.CallPrefix));

            if (cw is not null)
            {
                characterWebhooks.Add(cw);
            }
            else if (withRefMessage) // or find some other that was called by a reply
            {
                cw = channel.CharacterWebhooks.Find(cw => cw.Id == rm!.Author.Id);
                if (cw is not null) characterWebhooks.Add(cw);
            }

            // Add characters who hunt the user
            var hunters = channel.CharacterWebhooks.Where(w => w.HuntedUsers.Any(h => h.UserId == userMessage.Author.Id && h.Chance > chance)).ToList();
            if (hunters is not null && hunters.Count > 0)
            {
                foreach (var h in hunters)
                    if (!characterWebhooks.Contains(h)) characterWebhooks.Add(h);
            }

            // Add some random character (1) by channel's random reply chance
            if (channel.RandomReplyChance > chance)
            {
                var characters = channel.CharacterWebhooks.Where(w => w.Id != userMessage.Author.Id).ToList();
                if (characters.Count > 0)
                {
                    var someRandomCharacter = characters[@Random.Next(characters.Count)];
                    if (!characterWebhooks.Contains(someRandomCharacter))
                        characterWebhooks.Add(someRandomCharacter);
                }
            }

            // Add certain random characters by their personal random reply chance
            var randomCharacters = channel.CharacterWebhooks.Where(w => w.Id != userMessage.Author.Id && w.ReplyChance > chance).ToList();
            if (randomCharacters.Count > 0)
            {
                foreach (var rc in randomCharacters)
                    if (!characterWebhooks.Contains(rc)) characterWebhooks.Add(rc);
            }

            return characterWebhooks;
        }

        private static async Task TryToAddButtonsAsync(CharacterWebhook characterWebhook, ISocketMessageChannel channel, ulong messageId)
        {
            try
            {
                var message = await channel.GetMessageAsync(messageId);

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
            catch
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to add reaction-buttons to the character message.\nMake sure that bot has permission to add reactions in this channel, or disable this feature with `/update toggle-swipes enable:false` command.".ToInlineEmbed(Color.Red));
            }
        }

        private static async Task<bool> CanCallCaiCharacter(CharacterWebhook cw, SocketUserMessage userMessage, IntegrationsService integration)
        {
            if (integration.CaiClient is null)
            {
                await userMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} CharacterAI integration is not available".ToInlineEmbed(Color.Red));
                return false;
            }

            var caiToken = cw.Channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;

            if (string.IsNullOrWhiteSpace(caiToken))
            {
                await userMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} You have to specify a CharacterAI auth token for your server first!".ToInlineEmbed(Color.Red));
                return false;
            }

            return true;
        }

        private static async Task<bool> CanCallOpenAiCharacter(CharacterWebhook cw, SocketUserMessage userMessage)
        {
            string? openAiToken = cw.PersonalOpenAiApiToken ?? cw.Channel.Guild.GuildOpenAiApiToken ?? ConfigFile.DefaultOpenAiApiToken.Value;

            if (string.IsNullOrWhiteSpace(openAiToken))
            {
                await userMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} You have to specify an OpenAI API token for your server first!".ToInlineEmbed(Color.Red));
                return false;
            }

            return true;
        }

        private async Task HandleTextMessageException(SocketMessage message, Exception e)
        {
            LogException(new[] { e });
            var channel = message.Channel as SocketGuildChannel;
            var guild = channel?.Guild;
            await TryToReportInLogsChannel(_client, title: "Exception",
                                                    desc: $"In Guild `{guild?.Name} ({guild?.Id})`, Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                          $"User: {message.Author?.Username}\n" +
                                                          $"Message: {message.Content}",
                                                    content: e.ToString(),
                                                    color: Color.Red,
                                                    error: true);
        }
    }
}
