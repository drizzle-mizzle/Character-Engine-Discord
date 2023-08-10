using CharacterAI;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using CharacterEngineDiscord.Services;
using CharacterEngineDiscord.Models.Database;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngineDiscord.Handlers
{
    internal class ReactionsHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationsService _integration;

        public ReactionsHandler(IServiceProvider services)
        {
            _services = services;
            _integration = _services.GetRequiredService<IntegrationsService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            _client.ReactionAdded += (msg, channel, reaction) =>
            {
                Task.Run(async () => {
                    try { await HandleReactionAsync(msg, channel, reaction); }
                    catch (Exception e) { await HandleReactionException(channel, reaction, e); }
                });
                return Task.CompletedTask;
            };

            _client.ReactionRemoved += (msg, channel, reaction) =>
            {
                Task.Run(async () => {
                    try { await HandleReactionAsync(msg, channel, reaction); }
                    catch (Exception e) { await HandleReactionException(channel, reaction, e); }
                });
                return Task.CompletedTask;
            };
        }

        private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> rawMessage, Cacheable<IMessageChannel, ulong> discordChannel, SocketReaction reaction)
        {
            var user = reaction.User.Value;
            if (user is null) return;

            var userReacted = (SocketGuildUser)user;
            if (userReacted.IsBot) return;

            IUserMessage originalMessage;
            try { originalMessage = await rawMessage.DownloadAsync(); }
            catch { return; }

            if (!originalMessage.Author.IsWebhook) return;

            var db = new StorageContext();
            var channel = await db.Channels.FindAsync(discordChannel.Id);
            if (channel is null) return;

            var characterWebhook = channel.CharacterWebhooks.Find(cw => cw.Id == originalMessage.Author.Id);
            if (characterWebhook is null) return;

            if (reaction.Emote.Name == STOP_BTN.Name)
            {
                characterWebhook.SkipNextBotMessage = true;
                await db.SaveChangesAsync();
                return;
            }

            //if (reaction.Emote.Name == TRANSLATE_BTN.Name)
            //{
            //    _ = TranslateMessageAsync(message, currentChannel.Data.TranslateLanguage);
            //    return;
            //}

            bool userIsLastCaller = characterWebhook.LastDiscordUserCallerId == userReacted.Id;
            bool msgIsSwipable = originalMessage.Id == characterWebhook.LastCharacterDiscordMsgId;
            if (!(userIsLastCaller && msgIsSwipable)) return;

            if ((reaction.Emote?.Name == ARROW_LEFT.Name) && characterWebhook.CurrentSwipeIndex > 0)
            {   // left arrow
                if (await _integration.UserIsBanned(reaction, _client)) return;

                characterWebhook.CurrentSwipeIndex--;
                await db.SaveChangesAsync();
                await SwipeMessageAsync(originalMessage, characterWebhook.Id, userReacted);
            }
            else if (reaction.Emote?.Name == ARROW_RIGHT.Name)
            {   // right arrow
                if (await _integration.UserIsBanned(reaction, _client)) return;

                characterWebhook.CurrentSwipeIndex++;
                await db.SaveChangesAsync();
                await SwipeMessageAsync(originalMessage, characterWebhook.Id, userReacted);
            }
        }

        private async Task SwipeMessageAsync(IUserMessage characterMessage, ulong characterWebhookId, SocketGuildUser caller)
        {
            var db = new StorageContext();
            var characterWebhook = await db.CharacterWebhooks.FindAsync(characterWebhookId);
            if (characterWebhook is null) return;

            //Move it to the end of the queue
            _integration.RemoveEmojiRequestQueue.Remove(characterMessage.Id);
            _integration.RemoveEmojiRequestQueue.Add(characterMessage.Id, characterWebhook.Channel.Guild.BtnsRemoveDelay);

            _integration.WebhookClients.TryGetValue(characterWebhook.Id, out DiscordWebhookClient? webhookClient);
            if (webhookClient is null) return;

            Embed? quoteEmbed = characterMessage.Embeds?.FirstOrDefault() as Embed;

            // Check if fetching a new message, or just swiping among already available ones
            var availableCharacterResponses = _integration.AvailableCharacterResponses[characterWebhookId];
            if (availableCharacterResponses.Count < characterWebhook.CurrentSwipeIndex + 1) // fetch new
            {
                await webhookClient.ModifyMessageAsync(characterMessage.Id, msg =>
                {
                    msg.Content = null;
                    msg.Embeds = new List<Embed> { WAIT_MESSAGE.ToInlineEmbed(Color.Teal) };
                    msg.AllowedMentions = AllowedMentions.None;
                });

                CharacterResponse characterResponse;
                if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
                    characterResponse = await SwipeCaiCharaterResponseAsync(characterWebhook, _integration.CaiClient);
                else if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                    characterResponse = await SwipeOpenAiResponseAsync(characterWebhook, _integration.HttpClient);
                else return;
                
                if (!characterResponse.IsSuccessful)
                {
                    await webhookClient.ModifyMessageAsync(characterMessage.Id, msg =>
                    {
                        msg.Embeds = new List<Embed> { characterResponse.Text.ToInlineEmbed(Color.Red) };
                        msg.AllowedMentions = AllowedMentions.All;
                    });
                    return;
                }

                // Add to the storage
                _integration.AvailableCharacterResponses[characterWebhookId].Add(new()
                {
                    MessageUuId = characterResponse.CharacterMessageUuid!,
                    Text = characterResponse.Text,
                    ImageUrl = characterResponse.ImageRelPath
                });
            }

            AvailableCharacterResponse newCharacterMessage;
            try { newCharacterMessage = _integration.AvailableCharacterResponses[characterWebhookId][characterWebhook.CurrentSwipeIndex]; }
            catch { return; }

            characterWebhook.LastCharacterMsgUuId = newCharacterMessage.MessageUuId;

            // Add image or/and quote to the message
            var embeds = new List<Embed>();
            string? imageUrl = newCharacterMessage.ImageUrl;

            if (quoteEmbed is not null)
                embeds.Add(quoteEmbed);

            if (imageUrl is not null && await TryGetImageAsync(imageUrl, _integration.HttpClient))
                embeds.Add(new EmbedBuilder().WithImageUrl(imageUrl).Build());

            // Add text to message
            string responseText = newCharacterMessage.Text ?? " ";
            if (responseText.Length > 2000)
                responseText = responseText[0..1994] + "[...]";

            // Send (update) message
            await webhookClient.ModifyMessageAsync(characterMessage.Id, msg =>
            {
                msg.Content = $"{caller.Mention} {responseText}".Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{caller.Nickname ?? caller.GlobalName ?? caller.Username}**");
                msg.Embeds = embeds;
                msg.AllowedMentions = AllowedMentions.All;
            });

            if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
            {
                characterWebhook.OpenAiHistoryMessages.Remove(characterWebhook.OpenAiHistoryMessages.Last());
                characterWebhook.OpenAiHistoryMessages.Add(new() { Role = "assistant", Content = responseText, CharacterWebhookId = characterWebhookId });    
            }

            await db.SaveChangesAsync();
            //var tm = TranslatedMessages.Find(tm => tm.MessageId == message.Id);
            //if (tm is not null) tm.IsTranslated = false;
        }

        private static async Task<Models.Common.CharacterResponse> SwipeOpenAiResponseAsync(CharacterWebhook characterWebhook, HttpClient client)
        {
            var openAiParams = BuildChatOpenAiRequestPayload(characterWebhook, removeLastMessage: true);
            var openAiResponse = await CallChatOpenAiAsync(openAiParams, client);

            if (openAiResponse is null || openAiResponse.IsFailure)
            {
                return new()
                {
                    Text = openAiResponse?.ErrorReason ?? "Something went wrong!",
                    IsSuccessful = false,
                    CharacterMessageUuid = null, UserMessageId = null, ImageRelPath = null,
                };
            }
            else
            {
                characterWebhook.LastRequestTokensUsage = openAiResponse.Usage ?? 0;
                return new()
                {
                    Text = openAiResponse.Message!,
                    IsSuccessful = true,
                    CharacterMessageUuid = openAiResponse.MessageId,
                    UserMessageId = null, ImageRelPath = null,
                };
            }
        }

        private static async Task<Models.Common.CharacterResponse> SwipeCaiCharaterResponseAsync(CharacterWebhook characterWebhook, CharacterAIClient? client)
        {
            var caiToken = characterWebhook.Channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;
            if (string.IsNullOrWhiteSpace(caiToken))
            {
                return new()
                {
                    Text = $"{WARN_SIGN_DISCORD} You have to specify a CharacterAI auth token for your server first!",
                    IsSuccessful = false,
                    CharacterMessageUuid = null, ImageRelPath = null, UserMessageId = null
                };
            }

            var plusMode = characterWebhook.Channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
            var caiResponse = await client!.CallCharacterAsync(characterWebhook.Character.Id, characterWebhook.Character.Tgt!, characterWebhook.CaiActiveHistoryId!, parentMsgUuId: characterWebhook.LastUserMsgUuId, customAuthToken: caiToken, customPlusMode: plusMode);

            if (!caiResponse.IsSuccessful)
            {
                return new()
                {
                    Text = caiResponse.ErrorReason!.Replace(WARN_SIGN_UNICODE, WARN_SIGN_DISCORD),
                    IsSuccessful = false,
                    CharacterMessageUuid = null, UserMessageId = null, ImageRelPath = null,
                };
            }
            else
            {
                return new()
                {
                    Text = caiResponse.Response!.Text,
                    IsSuccessful = true,
                    CharacterMessageUuid = caiResponse.Response.UuId,
                    UserMessageId = caiResponse.LastUserMsgUuId,
                    ImageRelPath = caiResponse.Response.ImageRelPath,
                };
            }
        }

        private async Task HandleReactionException(Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction, Exception e)
        {
            LogException(new[] { e });
            var guildChannel = (await channel.GetOrDownloadAsync()) as SocketGuildChannel;
            var guild = guildChannel?.Guild;
            await TryToReportInLogsChannel(_client, title: "Exception",
                                                    desc: $"In Guild `{guild?.Name} ({guild?.Id})`, Channel: `{guildChannel?.Name} ({guildChannel?.Id})`\n" +
                                                          $"User: {reaction.User.Value?.Username}\n" +
                                                          $"Reaction: {reaction.Emote.Name}",
                                                    content: e.ToString(),
                                                    color: Color.Red,
                                                    error: true);
        }
    }
}
