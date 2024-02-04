using CharacterAI.Client;
using CharacterEngineDiscord.Interfaces;
using Discord;
using Discord.WebSocket;
using CharacterEngineDiscord.Services;
using CharacterEngineDiscord.Models.Database;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using CharacterEngineDiscord.Models.Common;
using Discord.Webhook;
using PuppeteerSharp.Helpers;

namespace CharacterEngineDiscord.Handlers
{
    internal class ReactionsHandler(IDiscordClient client, IIntegrationsService integrations)
    {
        public Task HandleReaction(Cacheable<IUserMessage, ulong> msg, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            Task.Run(async () =>
            {
                try { await HandleReactionAsync(msg, channel, reaction); }
                catch (NullReferenceException e) { LogException(e); }
                catch (Exception e) { await HandleReactionException(channel, reaction, e); }
            });

            return Task.CompletedTask;
        }

        private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> rawMessage, Cacheable<IMessageChannel, ulong> discordChannel, SocketReaction reaction)
        {
            var user = reaction.User.GetValueOrDefault(null);
            if (user is not SocketGuildUser userReacted || userReacted.IsBot) return;

            IUserMessage originalMessage;
            try { originalMessage = await rawMessage.DownloadAsync(); }
            catch { return; }
            if (originalMessage is null) return;
            if (!originalMessage.Author.IsWebhook) return;

            await using var db = new StorageContext();
            var channel = await db.Channels.FindAsync(discordChannel.Id);
            if (channel is null) return;

            var characterWebhook = channel.CharacterWebhooks.Find(cw => cw.Id == originalMessage.Author.Id);
            if (characterWebhook is null) return;

            if (reaction.Emote?.Name == STOP_BTN.Name)
            {
                characterWebhook.SkipNextBotMessage = true;
                await TryToSaveDbChangesAsync(db);
                return;
            }

            bool userIsLastCaller = characterWebhook.LastDiscordUserCallerId == userReacted.Id;
            bool msgIsSwipable = originalMessage.Id == characterWebhook.LastCharacterDiscordMsgId;
            if (!(userIsLastCaller && msgIsSwipable)) return;

            if ((reaction.Emote?.Name == ARROW_LEFT.Name) && characterWebhook.CurrentSwipeIndex > 0)
            {   // left arrow
                if (await integrations.UserIsBanned(reaction, client)) return;
                if (integrations.GuildIsAbusive(channel.GuildId))
                    await Task.Delay(2000);

                characterWebhook.CurrentSwipeIndex--;
                await TryToSaveDbChangesAsync(db);
                await SwipeCharacterMessageAsync(originalMessage, characterWebhook.Id, userReacted, isSwipe: true);
            }
            else if (reaction.Emote?.Name == ARROW_RIGHT.Name)
            {   // right arrow
                if (await integrations.UserIsBanned(reaction, client)) return;
                if (integrations.GuildIsAbusive(channel.GuildId))
                    await Task.Delay(2000);

                characterWebhook.CurrentSwipeIndex++;
                await TryToSaveDbChangesAsync(db);
                await SwipeCharacterMessageAsync(originalMessage, characterWebhook.Id, userReacted, isSwipe: true);
            }
            else if (reaction.Emote?.Name == CRUTCH_BTN.Name)
            {   // proceed generation
                if (await integrations.UserIsBanned(reaction, client)) return;
                if (integrations.GuildIsAbusive(channel.GuildId))
                    await Task.Delay(2000);

                await SwipeCharacterMessageAsync(originalMessage, characterWebhook.Id, userReacted, isSwipe: false);
            }
        }

        private async Task SwipeCharacterMessageAsync(IUserMessage characterOriginalMessage, ulong characterWebhookId, SocketGuildUser userCalled, bool isSwipe)
        {
            bool messageIsNotTracked = !integrations.Conversations.ContainsKey(characterWebhookId);
            if (messageIsNotTracked) return;

            await using var db = new StorageContext();
            var characterWebhook = await db.CharacterWebhooks.FindAsync(characterWebhookId);
            if (characterWebhook is null) return;

            var webhookClient = integrations.GetWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            if (webhookClient is null)
            {
                await characterOriginalMessage.Channel.SendMessageAsync(embed: $"{WARN_SIGN_DISCORD} Failed to update character message".ToInlineEmbed(Color.Red));
                return;
            }

            var convo = integrations.Conversations[characterWebhookId];
            await convo.Locker.WaitAsync();
            try
            {
                await ContinueSwipeAsync(convo, characterWebhook, userCalled, characterOriginalMessage, webhookClient, isSwipe, db);
            }
            finally
            {
                convo.Locker.Release();
                await TryToSaveDbChangesAsync(db);
            }
        }

        private async Task ContinueSwipeAsync(LastCharacterCall convo, CharacterWebhook cw, SocketGuildUser userCalled, IUserMessage characterOriginalMessage, DiscordWebhookClient webhookClient, bool isSwipe, StorageContext db)
        {
            // Remember quote from the message embed before it will be erased
            var quoteEmbed = characterOriginalMessage.Embeds?.FirstOrDefault(e => e.Footer is not null) as Embed;

            // Check if fetching a new message, or just swiping among already available ones
            // If fetch new, it will update convo.AvailableMessages
            bool gottaFetch = !isSwipe || (convo.AvailableMessages.Count < cw.CurrentSwipeIndex + 1);
            if (gottaFetch)
            { 
                await webhookClient.ModifyMessageAsync(characterOriginalMessage.Id, msg =>
                {
                    if (isSwipe) msg.Content = null;
                    msg.Embeds = new List<Embed> { WAIT_MESSAGE };
                    msg.AllowedMentions = AllowedMentions.None;
                });
                bool success = await TryToFetchNewCharacterMessageAsync(convo, cw, characterOriginalMessage, webhookClient, isSwipe);
                if (!success) return;
            }
            convo.LastUpdated = DateTime.UtcNow;

            AvailableCharacterResponse newCharacterMessage;
            try { newCharacterMessage = convo.AvailableMessages.ElementAt(cw.CurrentSwipeIndex); }
            catch { return; }

            cw.LastCharacterMsgId = newCharacterMessage.MessageId;
            cw.LastRequestTokensUsage = newCharacterMessage.TokensUsed;

            // Add image or/and quote to the message
            var embeds = new List<Embed>();
            string? imageUrl = newCharacterMessage.ImageUrl;

            if (quoteEmbed is not null)
                embeds.Add(quoteEmbed);

            if (imageUrl is not null && await CheckIfImageIsAvailableAsync(imageUrl, integrations.ImagesHttpClient))
                embeds.Add(new EmbedBuilder().WithImageUrl(imageUrl).Build());

            // Add text to the message
            string responseText = newCharacterMessage.Text ?? string.Empty;
            string newContent = $"{userCalled.Mention} {responseText}".Replace("{{user}}", $"**{userCalled.GetBestName()}**");
            if (newContent.Length > 2000)
                newContent = newContent[0..1994] + "[...]";

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

            // If message was swiped, replace the last one
            var type = cw.IntegrationType;
            if (type is IntegrationType.OpenAI)
            {
                cw.StoredHistoryMessages.Remove(cw.StoredHistoryMessages.Last());
                db.StoredHistoryMessages.Add(new() { Role = "assistant", Content = responseText, CharacterWebhookId = cw.Id });
            }
            else if (type is IntegrationType.KoboldAI || type is IntegrationType.HordeKoboldAI)
            {
                cw.StoredHistoryMessages.Remove(cw.StoredHistoryMessages.Last());
                db.StoredHistoryMessages.Add(new() { Role = $"\n<{cw.Character.Name}>\n", Content = responseText, CharacterWebhookId = cw.Id });
            }
        }

        private async Task<bool> TryToFetchNewCharacterMessageAsync(LastCharacterCall convo, CharacterWebhook cw, IUserMessage characterOriginalMessage, DiscordWebhookClient webhookClient, bool isSwipe)
        {
            Models.Common.CharacterResponse characterResponse;
            if (cw.IntegrationType is IntegrationType.CharacterAI)
            {
                while (integrations.CaiReloading)
                    await Task.Delay(5000);

                var id = Guid.NewGuid();
                integrations.RunningCaiTasks.Add(id);
                try { characterResponse = await SwipeCaiMessageAsync(cw, integrations.CaiClient!).WithTimeout(60000); }
                finally { integrations.RunningCaiTasks.Remove(id); }
            }
            else if (cw.IntegrationType is IntegrationType.OpenAI)
                characterResponse = await SwipeOpenAiMessageAsync(cw, integrations.CommonHttpClient, isSwipeOrContinue: isSwipe);
            else if (cw.IntegrationType is IntegrationType.KoboldAI)
                characterResponse = await SwipeKoboldAiMessageAsync(cw, integrations.CommonHttpClient);
            else if (cw.IntegrationType is IntegrationType.HordeKoboldAI)
                characterResponse = await SwipeHordeKoboldAiMessageAsync(cw, integrations.CommonHttpClient);
            else return false;

            if (!characterResponse.IsSuccessful)
            {
                await webhookClient.ModifyMessageAsync(characterOriginalMessage.Id, msg =>
                {
                    msg.Embeds = new List<Embed> { characterResponse.Text.ToInlineEmbed(Color.Red) };
                    msg.AllowedMentions = AllowedMentions.All;
                });
                return false;
            }

            // Add to the storage
            var newResponse = new AvailableCharacterResponse()
            {
                MessageId = characterResponse.CharacterMessageId!,
                Text = isSwipe ? characterResponse.Text : MentionRegex().Replace(characterOriginalMessage.Content, "") + " " + characterResponse.Text,
                ImageUrl = characterResponse.ImageRelPath,
                TokensUsed = characterResponse.TokensUsed
            };

            if (isSwipe)
                convo.AvailableMessages.Add(newResponse);
            else // continue-crutch
                convo.AvailableMessages[cw.CurrentSwipeIndex] = newResponse;

            return true;
        }

        private static async Task<Models.Common.CharacterResponse> SwipeKoboldAiMessageAsync(CharacterWebhook cw, HttpClient httpClient)
        {
            var koboldAiParams = BuildKoboldAiRequestPayload(cw, isSwipe: true);
            var koboldAiResponse = await SendKoboldAiRequestAsync(cw.Character.Name, koboldAiParams, httpClient, continueRequest: false);

            if (koboldAiResponse is null || koboldAiResponse.IsFailure)
            {
                return new()
                {
                    Text = koboldAiResponse?.ErrorReason ?? "Something went wrong!",
                    IsSuccessful = false
                };
            }
            else
            {
                return new()
                {
                    Text = koboldAiResponse.Message!,
                    IsSuccessful = true
                };
            }
        }

        private async Task<CharacterResponse> SwipeHordeKoboldAiMessageAsync(CharacterWebhook cw, HttpClient httpClient)
        {
            var hordeKoboldAiParams = BuildHordeKoboldAiRequestPayload(cw, isSwipe: true);
            var hordeKoboldAiResponse = await SendHordeKoboldAiRequestAsync(cw.Character.Name, hordeKoboldAiParams, httpClient, continueRequest: false);

            if (hordeKoboldAiResponse is null || hordeKoboldAiResponse.IsFailure || hordeKoboldAiResponse.Id.IsEmpty())
            {
                return new()
                {
                    Text = $"{WARN_SIGN_DISCORD} **Failed to fetch character response:** ```\n{hordeKoboldAiResponse?.ErrorReason ?? "Something went wrong"}\n```",
                    IsSuccessful = false
                };
            }
            else
            {
                var hordeResult = await TryToAwaitForHordeRequestResultAsync(hordeKoboldAiResponse.Id, integrations.CommonHttpClient, 0);

                string message;
                bool success;
                if (hordeResult.IsSuccessful)
                {
                    message = hordeResult.Message!;
                    success = true;
                }
                else
                {
                    message = hordeResult.ErrorReason!;
                    success = false;
                }

                return new()
                {
                    Text = message,
                    IsSuccessful = success
                };
            }
        }

        /// <param name="isSwipeOrContinue">true = swipe, false = continue</param>
        private static async Task<Models.Common.CharacterResponse> SwipeOpenAiMessageAsync(CharacterWebhook characterWebhook, HttpClient client, bool isSwipeOrContinue)
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

        private static async Task<Models.Common.CharacterResponse> SwipeCaiMessageAsync(CharacterWebhook characterWebhook, CharacterAiClient client)
        {
            var caiToken = characterWebhook.Channel.Guild.GuildCaiUserToken ?? string.Empty;
            var plusMode = characterWebhook.Channel.Guild.GuildCaiPlusMode ?? false;

            var caiResponse = await client.CallCharacterAsync(characterWebhook.Character.Id, characterWebhook.Character.Tgt!, characterWebhook.ActiveHistoryID!, parentMsgUuId: characterWebhook.LastUserMsgId, authToken: caiToken, plusMode: plusMode);

            if (!caiResponse.IsSuccessful)
            {
                return new()
                {
                    Text = caiResponse.ErrorReason!.Replace(WARN_SIGN_UNICODE, WARN_SIGN_DISCORD),
                    IsSuccessful = false
                };
            }
            else
            {
                return new()
                {
                    Text = caiResponse.Response!.Text,
                    IsSuccessful = true,
                    CharacterMessageId = caiResponse.Response.UuId,
                    UserMessageId = caiResponse.LastUserMsgUuId,
                    ImageRelPath = caiResponse.Response.ImageRelPath,
                };
            }
        }


        private async Task HandleReactionException(Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction, Exception e)
        {
            LogException(e);

            var guildChannel = (await channel.GetOrDownloadAsync()) as SocketGuildChannel;
            var guild = guildChannel?.Guild;

            TryToReportInLogsChannel(client, title: "Reaction Exception",
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
   