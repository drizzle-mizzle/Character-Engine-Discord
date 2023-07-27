using CharacterAI;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using CharacterEngineDiscord.Models.Database;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Models.Common;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Discord.Commands;

namespace CharacterEngineDiscord.Handlers
{
    internal class ButtonsAndReactionsHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationsService _integration;

        public ButtonsAndReactionsHandler(IServiceProvider services)
        {
            _services = services;
            _integration = _services.GetRequiredService<IntegrationsService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            _client.ButtonExecuted += (component) =>
            {
                Task.Run(async () => await HandleButtonAsync(component));
                return Task.CompletedTask;
            };

            _client.ReactionAdded += (msg, chanel, reaction) =>
            {
                Task.Run(async () => await HandleReactionAsync(msg, chanel, reaction));
                return Task.CompletedTask;
            };

            _client.ReactionRemoved += (msg, chanel, reaction) =>
            {
                Task.Run(async () => await HandleReactionAsync(msg, chanel, reaction));
                return Task.CompletedTask;
            };
        }

        private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> rawMessage, Cacheable<IMessageChannel, ulong> discordChannel, SocketReaction reaction)
        {
            var user = reaction.User.Value;
            if (user is null) return;

            var userReacted = (SocketGuildUser)user;
            if (userReacted.IsBot) return;

            var characterMessage = await rawMessage.DownloadAsync();
            if (!characterMessage.Author.IsWebhook) return;

            var db = _services.GetRequiredService<StorageContext>();
            var channel = await db.Channels.FindAsync(discordChannel.Id);
            if (channel is null) return;

            await db.Entry(channel).ReloadAsync();
            var characterWebhook = channel.CharacterWebhooks.Find(cw => cw.Id == characterMessage.Author.Id);
            if (characterWebhook is null) return;

            if (reaction.Emote.Name == STOP_BTN.Name)
            {
                characterWebhook.SkipNextBotMessage = true;
                return;
            }

            //if (reaction.Emote.Name == TRANSLATE_BTN.Name)
            //{
            //    _ = TranslateMessageAsync(message, currentChannel.Data.TranslateLanguage);
            //    return;
            //}

            bool userIsLastCaller = characterWebhook.LastDiscordUserCallerId == userReacted.Id;
            bool msgIsSwipable = characterMessage.Id == characterWebhook.LastCharacterDiscordMsgId;
            if (!(userIsLastCaller && msgIsSwipable)) return;

            if (reaction.Emote.Name == ARROW_LEFT.Name && characterWebhook.CurrentSwipeIndex > 0)
            {   // left arrow
                if (await _integration.UserIsBanned(reaction, _client, db)) return;

                characterWebhook.CurrentSwipeIndex--;
                await SwipeMessageAsync(characterMessage, characterWebhook, userReacted);
            }
            else if (reaction.Emote.Name == ARROW_RIGHT.Name)
            {   // right arrow
                if (await _integration.UserIsBanned(reaction, _client, db)) return;

                characterWebhook.CurrentSwipeIndex++;
                await SwipeMessageAsync(characterMessage, characterWebhook, userReacted);
            }
        }

        private async Task HandleButtonAsync(SocketMessageComponent component)
        {
            try
            {
                await component.DeferAsync();

                var searchQuery = _integration.SearchQueries.Find(sq => sq.ChannelId == component.ChannelId);
                if (searchQuery is null || searchQuery.SearchQueryData.IsEmpty) return;
                if (searchQuery.AuthorId != component.User.Id) return;

                var db = _services.GetRequiredService<StorageContext>();
                if (await UserIsBannedCheckOnly(component.User, db)) return;

                int tail = searchQuery.SearchQueryData.Characters.Count - (searchQuery.CurrentPage - 1) * 10;
                int maxRow = tail > 10 ? 10 : tail;

                switch (component.Data.CustomId)
                {
                    case "up":
                        if (searchQuery.CurrentRow == 1) searchQuery.CurrentRow = maxRow;
                        else searchQuery.CurrentRow--; break;
                    case "down":
                        if (searchQuery.CurrentRow > maxRow) searchQuery.CurrentRow = 1;
                        else searchQuery.CurrentRow++; break;
                    case "left":
                        searchQuery.CurrentRow = 1;
                        if (searchQuery.CurrentPage == 1) searchQuery.CurrentPage = searchQuery.Pages;
                        else searchQuery.CurrentPage--; break;
                    case "right":
                        searchQuery.CurrentRow = 1;
                        if (searchQuery.CurrentPage == searchQuery.Pages) searchQuery.CurrentPage = 1;
                        else searchQuery.CurrentPage++; break;
                    case "select":
                        await component.Message.ModifyAsync(msg =>
                        {
                            msg.Embed = InlineEmbed(WAIT_MESSAGE, Color.Teal);
                            msg.Components = null;
                        });

                        int index = (searchQuery.CurrentPage - 1) * 10 + searchQuery.CurrentRow - 1;
                        string characterId = searchQuery.SearchQueryData.Characters[index].Id;

                        Models.Database.Character? character;

                        if (searchQuery.SearchQueryData.IntegrationType is IntegrationType.CharacterAI)
                        {
                            character = await SelectCaiCharacterAsync(characterId, searchQuery.ChannelId);
                        }
                        else if (searchQuery.SearchQueryData.IntegrationType is IntegrationType.OpenAI)
                        {
                            var chubCharacter = await GetChubCharacterInfo(characterId, _integration.HttpClient);
                            character = CharacterFromChubCharacterInfo(chubCharacter);
                        }
                        else
                        {
                            return;
                        }
                        
                        if (character is null)
                        {
                            await component.Message.ModifyAsync(msg => msg.Embed = FailedToSetCharacterEmbed());
                            return;
                        }

                        var context = new InteractionContext(_client, component, component.Channel);

                        var characterWebhook = await CreateCharacterWebhookAsync(searchQuery.SearchQueryData.IntegrationType, context, character, db, _integration);
                        if (characterWebhook is null) return;

                        var webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
                        _integration.WebhookClients.Add(characterWebhook.Id, webhookClient);

                        await component.Message.ModifyAsync(msg => msg.Embed = SpawnCharacterEmbed(characterWebhook));
                        await webhookClient.SendMessageAsync($"{component.User.Mention} {character.Greeting}");

                        _integration.SearchQueries.Remove(searchQuery);
                        return;
                    default:
                        return;
                }

                // Only if left/right/up/down is selected, either this line will never be reached
                await component.Message.ModifyAsync(c => c.Embed = BuildCharactersList(searchQuery)).ConfigureAwait(false);
            }
            catch (Exception e) { LogException(new[] { e }); }
        }

        private async Task SwipeMessageAsync(IUserMessage characterMessage, CharacterWebhook characterWebhook, SocketGuildUser caller)
        {
            //Move it to the end of the queue
            _integration.RemoveEmojiRequestQueue.Remove(characterMessage.Id);
            _integration.RemoveEmojiRequestQueue.Add(characterMessage.Id, characterWebhook.Channel.Guild.BtnsRemoveDelay);

            _integration.WebhookClients.TryGetValue(characterWebhook.Id, out DiscordWebhookClient? webhookClient);
            if (webhookClient is null) return;

            Embed? quoteEmbed = characterMessage.Embeds?.FirstOrDefault() as Embed;
            // Check if fetching a new message, or just swiping among already available ones
            if (characterWebhook.AvailableCharacterResponses.Count < characterWebhook.CurrentSwipeIndex + 1) // fetch new
            {
                await webhookClient.ModifyMessageAsync(characterMessage.Id, msg =>
                {
                    msg.Content = null;
                    msg.Embeds = new List<Embed> { InlineEmbed(WAIT_MESSAGE, Color.Teal) };
                    msg.AllowedMentions = AllowedMentions.None;
                });

                CharacterResponse characterResponse;
                if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
                    characterResponse = await SwipeCaiCharaterResponseAsync(characterWebhook, _integration.CaiClient);
                else if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                {
                    var openAiParams = BuildChatOpenAiRequestPayload(characterWebhook);
                    var openAiResponse = await CallChatOpenAiAsync(openAiParams, _integration.HttpClient);
                    characterWebhook.LastRequestTokensUsage = openAiResponse.Usage ?? 0;
                    characterResponse = new(openAiResponse.Message!, openAiResponse.IsSuccessful, openAiResponse.MessageID, null);
                } else return;
                
                if (!characterResponse.IsSuccessful)
                {
                    await webhookClient.ModifyMessageAsync(characterMessage.Id, msg =>
                    {
                        msg.Embeds = new List<Embed> { InlineEmbed(characterResponse.Text, Color.Red) };
                        msg.AllowedMentions = AllowedMentions.All;
                    });
                    return;
                }

                // Add to the storage
                characterWebhook.AvailableCharacterResponses.Add(characterResponse.CharacterMessageId!, new(characterResponse.Text, characterResponse.ImageRelPath));
            }

            var newCharacterMessage = characterWebhook.AvailableCharacterResponses.ElementAt(characterWebhook.CurrentSwipeIndex);
            characterWebhook.LastCharacterMsgUuId = newCharacterMessage.Key;
            
            // Add image or/and quote to the message
            var embeds = new List<Embed>();
            var imageUrl = newCharacterMessage.Value.Value;

            if (quoteEmbed is not null)
                embeds.Add(quoteEmbed);

            if (imageUrl is not null && await TryGetImageAsync(imageUrl, _integration.HttpClient))
                embeds.Add(new EmbedBuilder().WithImageUrl(imageUrl).Build());

            // Add text to message
            string responseText = newCharacterMessage.Value.Key;
            if (responseText.Length > 2000)
                responseText = responseText[0..1994] + "[...]";

            // Send (update) message
            await webhookClient.ModifyMessageAsync(characterMessage.Id, msg =>
            {
                msg.Content = $"{caller.Mention} {responseText}".Replace("{{char}}", $"**{characterWebhook.Character.Name}**")
                                                                .Replace("{{user}}", $"**{caller.Nickname ?? caller.GlobalName ?? caller.Username}**");
                msg.Embeds = embeds;
                msg.AllowedMentions = AllowedMentions.All;
            });

            if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
            {
                characterWebhook.OpenAiHistoryMessages.Remove(characterWebhook.OpenAiHistoryMessages.Last());
                characterWebhook.OpenAiHistoryMessages.Add(new() { Role = "assistant", Content = newCharacterMessage.Value.Key, CharacterWebhookId = characterWebhook.Id });

                var db = _services.GetRequiredService<StorageContext>();
                await db.SaveChangesAsync();
            }
            //var tm = TranslatedMessages.Find(tm => tm.MessageId == message.Id);
            //if (tm is not null) tm.IsTranslated = false;
        }

        private static async Task<CharacterResponse> SwipeCaiCharaterResponseAsync(CharacterWebhook characterWebhook, CharacterAIClient? client)
        {
            var caiToken = characterWebhook.Channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;
            if (string.IsNullOrWhiteSpace(caiToken))
            {
                return new($"{WARN_SIGN_DISCORD} You have to specify a CharacterAI auth token for your server first!", false);
            }

            var plusMode = characterWebhook.Channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
            var response = await client!.CallCharacterAsync(characterWebhook.Character.Id, characterWebhook.Character.Tgt!, characterWebhook.CaiActiveHistoryId!, parentMsgUuId: characterWebhook.LastUserMsgUuId, customAuthToken: caiToken, customPlusMode: plusMode);
            var text = response.IsSuccessful ? response.Response!.Text : response.ErrorReason!.Replace(WARN_SIGN_UNICODE, WARN_SIGN_DISCORD);

            return new(text, response.IsSuccessful, response.Response?.UuId, response.Response?.ImageRelPath);
        }

        /// <summary>
        /// Called when user presses "select" button in search
        /// </summary>
        private async Task<Models.Database.Character?> SelectCaiCharacterAsync(string characterId, ulong channelId)
        {
            if (_integration.CaiClient is null) return null;

            var db = _services.GetRequiredService<StorageContext>();
            var channel = await db.Channels.FindAsync(channelId);
            if (channel is null) return null;

            await db.Entry(channel).ReloadAsync();
            var caiToken = channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;
            var plusMode = channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
            if (string.IsNullOrWhiteSpace(caiToken)) return null;

            var caiCharacter = await _integration.CaiClient.GetInfoAsync(characterId, customAuthToken: caiToken, customPlusMode: plusMode);
            return CharacterFromCaiCharacterInfo(caiCharacter);
        }
    }
}
