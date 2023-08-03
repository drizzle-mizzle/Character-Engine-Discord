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
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Reflection;
using System;

namespace CharacterEngineDiscord.Handlers
{
    internal class TextMessagesHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationsService _integration;
        private readonly InteractionService _interactions;

        public TextMessagesHandler(IServiceProvider services)
        {
            _services = services;
            _integration = _services.GetRequiredService<IntegrationsService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _interactions = _services.GetRequiredService<InteractionService>();

            _client.MessageReceived += (message) =>
            {
                Task.Run(async () => {
                    try
                    {
                        //if (message.Content == "##sync")
                        //    await TryToCreateSlashCommandsAsync(message);
                        //else
                            await HandleMessageAsync(message);
                    }
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

        private async Task TryToCreateSlashCommandsAsync(SocketMessage message)
        {
            var userMessage = (SocketUserMessage)message;
            var context = new SocketCommandContext(_client, userMessage);

            await context.Guild.DeleteApplicationCommandsAsync();
            await _interactions.RegisterCommandsToGuildAsync(context.Guild.Id);

            await userMessage.ReplyAsync(embed: SuccessEmbed());
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
                await userMessage.ReplyAsync(embed: $"{WARN_SIGN_DISCORD} You have to set backend API for this integration. Use `update api` command.".ToInlineEmbed(Color.Orange));

            if (!canProceed) return;

            // Reformat text message
            string text = userMessage.Content.RemovePrefix(characterWebhook.CallPrefix);
            text = characterWebhook.MessagesFormat.Replace("{{user}}", $"{userName}").Replace("{{msg}}", $"{text}");

            if (text.Contains("{{ref_msg_text}}"))
            {
                int start = text.IndexOf("{{ref_msg_begin}}");
                int end = text.IndexOf("{{ref_msg_end}}") + "{{ref_msg_end}}".Length;

                if (string.IsNullOrWhiteSpace(userMessage.ReferencedMessage?.Content))
                    text = text.Remove(start, end - start).Trim();
                else
                {
                    text = text.Replace("{{ref_msg_text}}", userMessage.ReferencedMessage!.Content.RemoveFirstMentionPrefx()).Replace("{{ref_msg_begin}}", "").Replace("{{ref_msg_end}}", "");
                }
            }


            // Get character response
            Models.Common.CharacterResponse? characterResponse = null;
            if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                characterResponse = await CallOpenAiCharacterAsync(characterWebhook, userMessage, text);
            else if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
                characterResponse = await CallCaiCharacterAsync(characterWebhook, userMessage, text);
                
            if (characterResponse is null) return;

            // Ensure webhook is being tracked
            if (!_integration.AvailableCharacterResponses.ContainsKey(characterWebhookId))
                _integration.AvailableCharacterResponses.Add(characterWebhookId, new());

            // Forget the choises from last message and remember new one
            _integration.AvailableCharacterResponses[characterWebhookId].Clear();
            _integration.AvailableCharacterResponses[characterWebhookId].Add(new()
            {
                Text = characterResponse.Text,
                MessageUuId = characterResponse.CharacterMessageUuid!,
                ImageUrl = characterResponse.ImageRelPath
            });

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
            characterMessage = $"{(userMessage.Author.IsWebhook ? $"**{userMessage.Author.Username}**," : userMessage.Author.Mention)} {characterMessage}";

            // Cut if too long
            if (characterMessage.Length > 2000)
                characterMessage = characterMessage[0..1994] + "[...]";

            // Fill embeds
            List<Embed>? embeds = new();
            if (characterWebhook.ReferencesEnabled)
            {
                int l = userMessage.Content.Length > 30 ? 30 : userMessage.Content.Length;
                embeds.Add(new EmbedBuilder().WithFooter($"> {userMessage.Content[0..l]}{(l == 30 ? "..." : "")}").Build());
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

            // Add swipe buttons
            var message = await AddArrowButtonsAsync(messageId, userMessage.Channel);
            _ = Task.Run(async () => await _integration.RemoveButtonsAsync(message, _client.CurrentUser, delay: characterWebhook.Channel.Guild.BtnsRemoveDelay));
        }

        private async Task<Models.Common.CharacterResponse?> CallOpenAiCharacterAsync(CharacterWebhook cw, SocketUserMessage userMessage, string text)
        {
            cw.OpenAiHistoryMessages.Add(new() { Role = "user", Content = text, CharacterWebhookId = cw.Id }); // remember user message (will be included in payload)

            var openAiRequestParams = BuildChatOpenAiRequestPayload(cw);
            var openAiResponse = await CallChatOpenAiAsync(openAiRequestParams, _integration.HttpClient);

            if (openAiResponse.IsFailure)
            {
                await userMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to fetch character response: `{openAiResponse.ErrorReason}`".ToInlineEmbed(Color.Red));
                return null;
            }

            // Remember character message
            cw.OpenAiHistoryMessages.Add(new() { Role = "assistant", Content = openAiResponse.Message!, CharacterWebhookId = cw.Id });
            cw.LastRequestTokensUsage = openAiResponse.Usage ?? 0;

            // Clear old messages, 40-60 is a good balance between response speed and needed context size, also it's usually pretty close to the GPT-3.5 token limit
            if (cw.OpenAiHistoryMessages.Count > 60)
                cw.OpenAiHistoryMessages.RemoveRange(0, 20);

            return new()
            {
                Text = openAiResponse.Message!,
                CharacterMessageUuid = openAiResponse.MessageId,
                IsSuccessful = true,
                UserMessageId = null, ImageRelPath = null,
            };
        }

        private async Task<Models.Common.CharacterResponse?> CallCaiCharacterAsync(CharacterWebhook cw, SocketUserMessage userMessage, string text)
        {
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
        private static async Task<List<Models.Database.CharacterWebhook>> DetermineCalledCharacterWebhook(SocketUserMessage userMessage, ulong channelId)
        {
            List<CharacterWebhook> characterWebhooks = new();

            var db = new StorageContext();
            var channel = await db.Channels.FindAsync(channelId);

            if (channel is null) return characterWebhooks;
            if (channel.CharacterWebhooks.Count == 0) return characterWebhooks;

            var text = userMessage.Content.Trim();
            var rm = userMessage.ReferencedMessage;
            var withRefMessage = rm is not null && rm.Author.IsWebhook;

            // One certain character that was
            if (withRefMessage) // called by reply
            {
                var cw = channel.CharacterWebhooks.Find(cw => cw.Id == rm!.Author.Id);
                if (cw is not null) characterWebhooks.Add(cw);
            }
            else // or called by a prefix
            {
                var cw = channel.CharacterWebhooks.FirstOrDefault(w => text.StartsWith(w.CallPrefix));
                if (cw is not null) characterWebhooks.Add(cw);
            }

            var chance = (float)(@Random.Next(99) + 0.001 + @Random.NextDouble());
            // Add characters who hunt the user
            var huntingWebhooks = channel.CharacterWebhooks.Where(w => w.HuntedUsers.Any(h => h.Id == userMessage.Author.Id && h.Chance > chance)).ToList();
            
            if (huntingWebhooks is not null && huntingWebhooks.Count > 0)
            {
                foreach (var cw in huntingWebhooks)
                    if (!characterWebhooks.Contains(cw)) characterWebhooks.Add(cw);
            }

            // Add some random characters            
            bool respondOnRandom = channel.RandomReplyChance > chance;
            if (respondOnRandom)
            {
                var cw = channel.CharacterWebhooks[@Random.Next(channel.CharacterWebhooks.Count)];
                if (!characterWebhooks.Contains(cw)) characterWebhooks.Add(cw);
            }

            return characterWebhooks;
        }

        private static async Task<IMessage> AddArrowButtonsAsync(ulong messageId, ISocketMessageChannel channel)
        {
            var message = await channel.GetMessageAsync(messageId);
            await message.AddReactionAsync(ARROW_LEFT);
            await message.AddReactionAsync(ARROW_RIGHT);

            return message;
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
