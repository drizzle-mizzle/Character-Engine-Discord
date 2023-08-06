using Discord;
using Discord.Webhook;
using Discord.Commands;
using Discord.WebSocket;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.CommandsService;
using CharacterEngineDiscord.Models.Database;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Models.Common;
using Discord.Interactions;
using System.Configuration;
using System.Threading.Channels;

namespace CharacterEngineDiscord.Handlers
{
    internal class TextMessagesHandler
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
                    catch (Exception e)
                    {
                        LogException(new[] { e });
                        var channel = message.Channel as SocketGuildChannel;
                        var guild = channel?.Guild;
                        await TryToReportInLogsChannel(_client, title: "Exception",
                                                                desc: $"In Guild `{guild?.Name} ({guild?.Id})`, Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                                      $"User: {message.Author?.Username}\n" +
                                                                      $"Message: {message.Content}\n" +
                                                                      $"```cs\n" +
                                                                      $"{e}\n" +
                                                                      $"```",
                                                                color: Color.Red);
                    }
                });
                return Task.CompletedTask;
            };
        }

        internal async Task HandleMessageAsync(SocketMessage sm)
        {
            if (sm is not SocketUserMessage userMessage) return;
            if (userMessage.Author.Id == _client.CurrentUser.Id) return;
            if (string.IsNullOrWhiteSpace(userMessage.Content)) return;

            var context = new SocketCommandContext(_client, userMessage);
            if (context.Guild is null) return;

            // Get stored channel and its' characters
            var characterWebhooks = await DetermineCalledCharacterWebhook(userMessage, context.Channel.Id);

            if (characterWebhooks.Count == 0) return;
            if (await _integration.UserIsBanned(context)) return;

            foreach (var characterWebhook in characterWebhooks)
            {
                int delay = characterWebhook.ResponseDelay;

                if ((userMessage.Author.IsWebhook || userMessage.Author.IsBot) && delay < 5)
                {
                    delay = 5;
                }

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
                userName = guildUser.Nickname ?? guildUser.DisplayName ?? guildUser.Username;
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
                await userMessage.ReplyAsync(embed: $"{WARN_SIGN_DISCORD} You have to set backend API for this integration. Use `/update api` command.".ToInlineEmbed(Color.Orange));

            if (!canProceed) return;

            // Reformat message
            string text = userMessage.Content ?? "";
            text = characterWebhook.MessagesFormat.Replace("{{user}}", $"{userName}").Replace("{{msg}}", $"{text.RemovePrefix(characterWebhook.CallPrefix)}");

            if (text.Contains("{{ref_msg_text}}"))
            {
                int start = text.IndexOf("{{ref_msg_begin}}");
                int end = text.IndexOf("{{ref_msg_end}}") + "{{ref_msg_end}}".Length;

                if (string.IsNullOrWhiteSpace(userMessage.ReferencedMessage?.Content))
                    text = text.Remove(start, end - start).Trim();
                else
                {
                    string refText = userMessage.ReferencedMessage.Content;
                    int refL = Math.Min(refText.Length, 350);

                    text = text.Replace("{{ref_msg_text}}", refText[0..refL] + (refL == 350 ? "..." : "")).Replace("{{ref_msg_user}}", userMessage.ReferencedMessage.Author.Username).Replace("{{ref_msg_begin}}", "").Replace("{{ref_msg_end}}", "");
                }
            }

            // Get character response
            CharacterResponse? characterResponse = null;
            if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                characterResponse = await CallOpenAiCharacterAsync(characterWebhookId, userMessage, text);
            else if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
                characterResponse = await CallCaiCharacterAsync(characterWebhookId, userMessage, text);

            if (characterResponse is null) return;

            // Ensure webhook is being tracked
            if (!_integration.AvailableCharacterResponses.ContainsKey(characterWebhookId))
                _integration.AvailableCharacterResponses.Add(characterWebhookId, new());

            // Forget the choises from last message and remember new one
            _integration.AvailableCharacterResponses[characterWebhookId].Clear();
            _integration.AvailableCharacterResponses[characterWebhookId].Add(new()
            {
                Text = characterResponse.Text,
                MessageUuId = characterResponse.CharacterMessageUuid,
                ImageUrl = characterResponse.ImageRelPath
            });

            await db.Entry(characterWebhook).ReloadAsync();
            characterWebhook.CurrentSwipeIndex = 0;
            characterWebhook.LastCharacterMsgUuId = characterResponse.CharacterMessageUuid;
            characterWebhook.LastUserMsgUuId = characterResponse.UserMessageId;
            characterWebhook.LastDiscordUserCallerId = userMessage.Author.Id;

            // Ensure webhook client does exist
            _integration.WebhookClients.TryGetValue(characterWebhook.Id, out DiscordWebhookClient? webhookClient);
            if (webhookClient is null)
            {
                webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
                _integration.WebhookClients.Add(characterWebhook.Id, webhookClient);
            }

            // Reformat message
            string characterMessage = characterResponse.Text.Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{userName}**");
            characterMessage = $"{(userMessage.Author.IsWebhook ? $"**{userMessage.Author.Username}**" : userMessage.Author.Mention)}, {characterMessage}";

            // Cut if too long
            if (characterMessage.Length > 2000)
                characterMessage = characterMessage[0..1994] + "[...]";

            // Fill embeds
            List<Embed>? embeds = new();
            if (characterWebhook.ReferencesEnabled && userMessage.Content is not null)
            {
                int l = Math.Min(userMessage.Content.Length, 40);
                embeds.Add(new EmbedBuilder().WithFooter($"> {userMessage.Content[0..l]}{(l == 40 ? "..." : "")}").Build());
            }
            if (characterResponse.ImageRelPath is not null)
            {
                bool canGetImage = await TryGetImageAsync(characterResponse.ImageRelPath, _integration.HttpClient);
                if (canGetImage) embeds.Add(new EmbedBuilder().WithImageUrl(characterResponse.ImageRelPath).Build());
            }

            // Send message
            var messageId = await webhookClient.SendMessageAsync(characterMessage, embeds: embeds);
            characterWebhook.LastCharacterDiscordMsgId = messageId;

            await db.SaveChangesAsync();

            if (userMessage.Author.IsWebhook || userMessage.Author.IsBot)
            {
                return;
            }
            // Add swipe buttons
            if (characterWebhook.SwipesEnabled)
            {
                var message = await userMessage.Channel.GetMessageAsync(messageId);
                if (message is null) return;

                var removeArrowButtonsAction = new Action(async ()
                    => await _integration.RemoveButtonsAsync(message, _client.CurrentUser, delay: characterWebhook.Channel.Guild.BtnsRemoveDelay));

                await AddArrowButtonsAsync(message, userMessage.Channel, removeArrowButtonsAction);
            }
        }

        private async Task<CharacterResponse?> CallOpenAiCharacterAsync(ulong cwId, SocketUserMessage userMessage, string text)
        {
            var db = new StorageContext();
            var cw = await db.CharacterWebhooks.FindAsync(cwId);
            if (cw is null) return null;

            cw.OpenAiHistoryMessages.Add(new() { Role = "user", Content = text, CharacterWebhookId = cw.Id }); // remember user message (will be included in payload)

            var openAiRequestParams = BuildChatOpenAiRequestPayload(cw);
            var openAiResponse = await CallChatOpenAiAsync(openAiRequestParams, _integration.HttpClient);

            if (openAiResponse.IsFailure)
            {
                await userMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to fetch character response: `{openAiResponse.ErrorReason}`".ToInlineEmbed(Color.Red));
                await db.SaveChangesAsync();
                return null;
            }

            // Remember character message
            cw.OpenAiHistoryMessages.Add(new() { Role = "assistant", Content = openAiResponse.Message!, CharacterWebhookId = cw.Id });
            cw.LastRequestTokensUsage = openAiResponse.Usage ?? 0;

            // Clear old messages, 40-60 is a good balance between response speed and needed context size, also it's usually pretty close to the GPT-3.5 token limit
            if (cw.OpenAiHistoryMessages.Count > 60)
                cw.OpenAiHistoryMessages.RemoveRange(0, 20);

            await db.SaveChangesAsync();

            return new()
            {
                Text = openAiResponse.Message!,
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
            var hunters = channel.CharacterWebhooks.Where(w => w.HuntedUsers.Any(h => h.Id == userMessage.Author.Id && h.Chance > chance)).ToList();            
            if (hunters is not null && hunters.Count > 0)
            {
                foreach (var h in hunters)
                    if (!characterWebhooks.Contains(h)) characterWebhooks.Add(h);
            }

            // Add some random character
            if (channel.RandomReplyChance > chance)
            {
                var randomCharacters = channel.CharacterWebhooks.Where(w => w.Id != userMessage.Author.Id).ToList();
                if (randomCharacters.Count > 0)
                {
                    var rw = randomCharacters[@Random.Next(randomCharacters.Count)];
                    if (!characterWebhooks.Contains(rw)) characterWebhooks.Add(rw);
                }
            }

            return characterWebhooks;
        }

        private static async Task AddArrowButtonsAsync(IMessage message, ISocketMessageChannel channel, Action removeReactions)
        {
            try
            {
                await message.AddReactionAsync(ARROW_LEFT);
                await message.AddReactionAsync(ARROW_RIGHT);
                _ = Task.Run(removeReactions);
            }
            catch
            {
                await channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to add swipe reaction-buttons to the character message.\nMake sure that bot has permission to manage reactions in this channel, or disable this feature with `/update swipes enable:false` command.".ToInlineEmbed(Color.Red));
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
    }
}
