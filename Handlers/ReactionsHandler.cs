using CharacterAI;
using Discord;
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
                Task.Run(async () =>
                {
                    try { await HandleReactionAsync(msg, channel, reaction); }
                    catch (Exception e) { await HandleReactionException(channel, reaction, e); }
                });
                return Task.CompletedTask;
            };

            _client.ReactionRemoved += (msg, channel, reaction) =>
            {
                Task.Run(async () =>
                {
                    try { await HandleReactionAsync(msg, channel, reaction); }
                    catch (Exception e) { await HandleReactionException(channel, reaction, e); }
                });
                return Task.CompletedTask;
            };
        }

        private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong>? rawMessage, Cacheable<IMessageChannel, ulong>? discordChannel, SocketReaction? reaction)
        {
            if (rawMessage is null || discordChannel is null || reaction is null) return;

            var user = reaction.User.GetValueOrDefault(null);
            if (user is null || user is not SocketGuildUser userReacted || userReacted.IsBot) return;

            IUserMessage originalMessage;
            try { originalMessage = await ((Cacheable<IUserMessage, ulong>)rawMessage).DownloadAsync(); }
            catch { return; }

            if (!originalMessage.Author.IsWebhook) return;

            var db = new StorageContext();
            var channel = await db.Channels.FindAsync(((Cacheable<IMessageChannel, ulong>)discordChannel).Id);
            if (channel is null) return;

            var characterWebhook = channel.CharacterWebhooks.Find(cw => cw.Id == originalMessage.Author.Id);
            if (characterWebhook is null) return;

            //if (reaction.Emote?.Name == STOP_BTN.Name)
            //{
            //    characterWebhook.SkipNextBotMessage = true;
            //    await db.SaveChangesAsync();
            //    return;
            //}

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
                await UpdateCharacterMessage(originalMessage, characterWebhook.Id, userReacted, isSwipe: true);
            }
            else if (reaction.Emote?.Name == ARROW_RIGHT.Name)
            {   // right arrow
                if (await _integration.UserIsBanned(reaction, _client)) return;

                characterWebhook.CurrentSwipeIndex++;
                await db.SaveChangesAsync();
                await UpdateCharacterMessage(originalMessage, characterWebhook.Id, userReacted, isSwipe: true);
            }
            else if (reaction.Emote?.Name == CRUTCH_BTN.Name)
            {   // proceed generation
                if (await _integration.UserIsBanned(reaction, _client)) return;

                await UpdateCharacterMessage(originalMessage, characterWebhook.Id, userReacted, isSwipe: false);
            }
        }

        /// <summary>
        /// Super complicated shit, but I don't want to refactor it...
        /// </summary>
        private async Task UpdateCharacterMessage(IUserMessage characterOriginalMessage, ulong characterWebhookId, SocketGuildUser caller, bool isSwipe)
        {
            if (!_integration.Conversations.ContainsKey(characterWebhookId)) return;

            var db = new StorageContext();
            var characterWebhook = await db.CharacterWebhooks.FindAsync(characterWebhookId);
            if (characterWebhook is null) return;

            var webhookClient = _integration.GetWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            if (webhookClient is null)
            {
                await characterOriginalMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to update character message".ToInlineEmbed(Color.Red));
                return;
            }

            var quoteEmbed = characterOriginalMessage.Embeds?.FirstOrDefault(e => e.Footer is not null) as Embed; // remember quote
            var convo = _integration.Conversations[characterWebhookId];

            // Check if fetching a new message, or just swiping among already available ones
            bool gottaFetch = !isSwipe || (convo.AvailableMessages.Count < characterWebhook.CurrentSwipeIndex + 1);

            if (gottaFetch)
            {
                await webhookClient.ModifyMessageAsync(characterOriginalMessage.Id, msg =>
                {
                    if (isSwipe) msg.Content = null;
                    msg.Embeds = new List<Embed> { WAIT_MESSAGE };
                    msg.AllowedMentions = AllowedMentions.None;
                });

                CharacterResponse characterResponse;
                if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
                    characterResponse = await SwipeCharacterAiMessageAsync(characterWebhook, _integration.CaiClient);
                else if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                    characterResponse = await SwipeOpenAiMessageAsync(characterWebhook, _integration.CommonHttpClient, isSwipeOrContinue: isSwipe);
                else if (characterWebhook.IntegrationType is IntegrationType.KoboldAI)
                    characterResponse = await SwipeKoboldAiMessageAsync(characterWebhook, _integration.CommonHttpClient);
                else if (characterWebhook.IntegrationType is IntegrationType.KoboldAI)
                    return; //characterResponse = await SwipeHordeKoboldAiMessageAsync(characterWebhook, _integration.CommonHttpClient, isSwipeOrContinue: isSwipe);
                else return;

                if (!characterResponse.IsSuccessful)
                {
                    await webhookClient.ModifyMessageAsync(characterOriginalMessage.Id, msg =>
                    {
                        msg.Embeds = new List<Embed> { characterResponse.Text.ToInlineEmbed(Color.Red) };
                        msg.AllowedMentions = AllowedMentions.All;
                    });
                    return;
                }

                // Add to the storage
                var newResponse = new AvailableCharacterResponse()
                {
                    MessageId = characterResponse.CharacterMessageId!,
                    Text = isSwipe ? characterResponse.Text : MentionRegex().Replace(characterOriginalMessage.Content, "") + " " + characterResponse.Text,
                    ImageUrl = characterResponse.ImageRelPath,
                    TokensUsed = characterResponse.TokensUsed
                };

                lock (convo.AvailableMessages)
                {
                    if (isSwipe)
                        convo.AvailableMessages.Add(newResponse);
                    else
                        convo.AvailableMessages[characterWebhook.CurrentSwipeIndex] = newResponse;
                }
            }

            convo.LastUpdated = DateTime.UtcNow;

            AvailableCharacterResponse newCharacterMessage;
            try { newCharacterMessage = convo.AvailableMessages[characterWebhook.CurrentSwipeIndex]; }
            catch { return; }

            characterWebhook.LastCharacterMsgId = newCharacterMessage.MessageId;
            characterWebhook.LastRequestTokensUsage = newCharacterMessage.TokensUsed;

            // Add image or/and quote to the message
            var embeds = new List<Embed>();
            string? imageUrl = newCharacterMessage.ImageUrl;

            if (quoteEmbed is not null)
                embeds.Add(quoteEmbed);

            if (imageUrl is not null && await ImageIsAvailable(imageUrl, _integration.ImagesHttpClient))
                embeds.Add(new EmbedBuilder().WithImageUrl(imageUrl).Build());

            // Add text to the message
            string responseText = newCharacterMessage.Text ?? " ";
            if (responseText.Length > 2000)
                responseText = responseText[0..1994] + "[...]";

            // Send (update) message
            string newContent = $"{caller.Mention} {responseText}".Replace("{{user}}", $"**{caller.GetBestName()}**");
            if (newContent.Length > 2000) newContent = newContent[0..1994] + "[max]";

            try
            {
                await webhookClient.ModifyMessageAsync(characterOriginalMessage.Id, msg =>
                {
                    msg.Content = newContent;
                    msg.Embeds = embeds;
                    msg.AllowedMentions = AllowedMentions.All;
                });
            }
            catch
            {
                return;
            }

            // If message was swiped, "forget" last option
            if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
            {
                characterWebhook.StoredHistoryMessages.Remove(characterWebhook.StoredHistoryMessages.Last());
                db.StoredHistoryMessages.Add(new() { Role = "assistant", Content = responseText, CharacterWebhookId = characterWebhookId });
            }

            await db.SaveChangesAsync();
            //var tm = TranslatedMessages.Find(tm => tm.MessageId == message.Id);
            //if (tm is not null) tm.IsTranslated = false;
        }

        private static async Task<CharacterResponse> SwipeKoboldAiMessageAsync(CharacterWebhook characterWebhook, HttpClient httpClient)
        {
            var koboldAiParams = BuildKoboldAiRequestPayload(characterWebhook, isSwipe: true);
            var koboldAiResponse = await SendKoboldAiRequestAsync(koboldAiParams, httpClient, continueRequest: false);

            if (koboldAiResponse is null || koboldAiResponse.IsFailure)
            {
                return new()
                {
                    Text = koboldAiResponse?.ErrorReason ?? "Something went wrong!",
                    IsSuccessful = false,
                    TokensUsed = 0,
                };
            }
            else
            {
                return new()
                {
                    Text = koboldAiResponse.Message!,
                    IsSuccessful = true,
                    TokensUsed = 0
                };
            }
        }

        //private Task<CharacterResponse> SwipeHordeKoboldAiMessageAsync(CharacterWebhook characterWebhook, HttpClient httpClient, bool isSwipeOrContinue)
        //{

        //}

        /// <param name="isSwipeOrContinue">true = swipe, false = continue</param>
        private static async Task<CharacterResponse> SwipeOpenAiMessageAsync(CharacterWebhook characterWebhook, HttpClient client, bool isSwipeOrContinue)
        {
            var openAiParams = BuildChatOpenAiRequestPayload(characterWebhook, isSwipe: isSwipeOrContinue, isContinue: !isSwipeOrContinue);
            var openAiResponse = await SendOpenAiRequestAsync(openAiParams, client);

            if (openAiResponse is null || openAiResponse.IsFailure)
            {
                return new()
                {
                    Text = openAiResponse?.ErrorReason ?? "Something went wrong!",
                    TokensUsed = 0,
                    IsSuccessful = false,
                };
            }
            else
            {
                return new()
                {
                    Text = openAiResponse.Message!,
                    IsSuccessful = true,
                    TokensUsed = openAiResponse.Usage ?? 0,
                    CharacterMessageId = openAiResponse.MessageId,
                };
            }
        }

        private static async Task<CharacterResponse> SwipeCharacterAiMessageAsync(CharacterWebhook characterWebhook, CharacterAIClient? client)
        {
            var caiToken = characterWebhook.Channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;
            
            if (string.IsNullOrWhiteSpace(caiToken))
            {
                return new()
                {
                    Text = $"{WARN_SIGN_DISCORD} You have to specify a CharacterAI auth token for your server first!",
                    TokensUsed = 0,
                    IsSuccessful = false
                };
            }

            var plusMode = characterWebhook.Channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
            var caiResponse = await client!.CallCharacterAsync(characterWebhook.Character.Id, characterWebhook.Character.Tgt!, characterWebhook.ActiveHistoryID!, parentMsgUuId: characterWebhook.LastUserMsgId, customAuthToken: caiToken, customPlusMode: plusMode);

            if (!caiResponse.IsSuccessful)
            {
                return new()
                {
                    Text = caiResponse.ErrorReason!.Replace(WARN_SIGN_UNICODE, WARN_SIGN_DISCORD),
                    TokensUsed = 0,
                    IsSuccessful = false
                };
            }
            else
            {
                return new()
                {
                    Text = caiResponse.Response!.Text,
                    TokensUsed = 0,
                    IsSuccessful = true,
                    CharacterMessageId = caiResponse.Response.UuId,
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
            TryToReportInLogsChannel(_client, title: "Reaction Exception",
                                              desc: $"Guild: `{guild?.Name} ({guild?.Id})`\n" +
                                                    $"Owner: `{guild?.Owner.GetBestName()} ({guild?.Owner.Username})`\n" +
                                                    $"Channel: `{guildChannel?.Name} ({guildChannel?.Id})`\n" +
                                                    $"User: `{reaction.User.GetValueOrDefault()?.Username}`\n" +
                                                    $"Reaction: {reaction.Emote.Name}",
                                              content: e.ToString(),
                                              color: Color.Red,
                                              error: true);
        }
    }
}
   