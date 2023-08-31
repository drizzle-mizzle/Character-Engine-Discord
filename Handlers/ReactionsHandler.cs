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
            var user = reaction.User.GetValueOrDefault();
            if (user is null || user is not SocketGuildUser userReacted || userReacted.IsBot) return;

            IUserMessage originalMessage;
            try { originalMessage = await rawMessage.DownloadAsync(); }
            catch { return; }

            if (originalMessage is null || originalMessage.Author is null) return;
            if (!originalMessage.Author.IsWebhook) return;

            var db = new StorageContext();
            var channel = await db.Channels.FindAsync(discordChannel.Id);
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
            var availResponses = _integration.AvailableCharacterResponses;
            if (!availResponses.ContainsKey(characterWebhookId)) return;

            var db = new StorageContext();
            var characterWebhook = await db.CharacterWebhooks.FindAsync(characterWebhookId);
            if (characterWebhook is null) return;

            var webhookClient = _integration.GetWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            if (webhookClient is null)
            {
                await characterOriginalMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to update character message".ToInlineEmbed(Color.Red));
                return;
            }

            // Remember quote
            Embed? quoteEmbed = characterOriginalMessage.Embeds?.FirstOrDefault() as Embed;

            // Check if fetching a new message, or just swiping among already available ones
            bool gottaFetch = !isSwipe || (availResponses[characterWebhookId].Count < characterWebhook.CurrentSwipeIndex + 1);
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
                    characterResponse = await GetCaiCharaterResponseAsync(characterWebhook, _integration.CaiClient);
                else if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                    characterResponse = await GetOpenAiResponseAsync(characterWebhook, _integration.HttpClient, isSwipeOrContinue: isSwipe);
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
                    MessageId = characterResponse.CharacterMessageUuid!,
                    Text = isSwipe ? characterResponse.Text : MentionRegex().Replace(characterOriginalMessage.Content, "") + " " + characterResponse.Text,
                    ImageUrl = characterResponse.ImageRelPath,
                    TokensUsed = characterResponse.TokensUsed
                };

                lock (availResponses)
                {
                    if (isSwipe)
                        availResponses[characterWebhookId].Add(newResponse);
                    else
                        availResponses[characterWebhookId][characterWebhook.CurrentSwipeIndex] = newResponse;
                }
            }

            AvailableCharacterResponse newCharacterMessage;
            try { newCharacterMessage = availResponses[characterWebhookId][characterWebhook.CurrentSwipeIndex]; }
            catch { return; }
            
            characterWebhook.LastCharacterMsgUuId = newCharacterMessage.MessageId;
            characterWebhook.LastRequestTokensUsage = newCharacterMessage.TokensUsed;

            // Add image or/and quote to the message
            var embeds = new List<Embed>();
            string? imageUrl = newCharacterMessage.ImageUrl;

            if (quoteEmbed is not null)
                embeds.Add(quoteEmbed);

            if (imageUrl is not null && await TryGetImageAsync(imageUrl, _integration.HttpClient))
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
                characterWebhook.OpenAiHistoryMessages.Remove(characterWebhook.OpenAiHistoryMessages.Last());
                db.OpenAiHistoryMessages.Add(new() { Role = "assistant", Content = responseText, CharacterWebhookId = characterWebhookId });    
            }

            await db.SaveChangesAsync();
            //var tm = TranslatedMessages.Find(tm => tm.MessageId == message.Id);
            //if (tm is not null) tm.IsTranslated = false;
        }

        
        /// <param name="isSwipeOrContinue">true = swipe, false = continue</param>
        private static async Task<CharacterResponse> GetOpenAiResponseAsync(CharacterWebhook characterWebhook, HttpClient client, bool isSwipeOrContinue)
        {
            var openAiParams = BuildChatOpenAiRequestPayload(characterWebhook, isSwipe: isSwipeOrContinue, isContinue: !isSwipeOrContinue);
            var openAiResponse = await CallChatOpenAiAsync(openAiParams, client);

            if (openAiResponse is null || openAiResponse.IsFailure)
            {
                return new()
                {
                    Text = openAiResponse?.ErrorReason ?? "Something went wrong!",
                    TokensUsed = 0,
                    IsSuccessful = false,
                    CharacterMessageUuid = null, UserMessageId = null, ImageRelPath = null,
                };
            }
            else
            {
                return new()
                {
                    Text = openAiResponse.Message!,
                    IsSuccessful = true,
                    TokensUsed = openAiResponse.Usage ?? 0,
                    CharacterMessageUuid = openAiResponse.MessageId,
                    UserMessageId = null, ImageRelPath = null
                };
            }
        }

        private static async Task<CharacterResponse> GetCaiCharaterResponseAsync(CharacterWebhook characterWebhook, CharacterAIClient? client)
        {
            var caiToken = characterWebhook.Channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;
            if (string.IsNullOrWhiteSpace(caiToken))
            {
                return new()
                {
                    Text = $"{WARN_SIGN_DISCORD} You have to specify a CharacterAI auth token for your server first!",
                    TokensUsed = 0,
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
                    TokensUsed = 0,
                    IsSuccessful = false,
                    CharacterMessageUuid = null, UserMessageId = null, ImageRelPath = null,
                };
            }
            else
            {
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
