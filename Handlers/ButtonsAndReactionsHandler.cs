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
using static CharacterEngineDiscord.Services.StorageContext;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.CharacterHub;
using System.Xml.Linq;
using Castle.Components.DictionaryAdapter.Xml;
using static System.Net.Mime.MediaTypeNames;

namespace CharacterEngineDiscord.Handlers
{
    internal class ButtonsAndReactionsHandler
    {
        private readonly StorageContext _db;
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationsService _integration;

        public ButtonsAndReactionsHandler(IServiceProvider services)
        {
            _services = services;
            _db = _services.GetRequiredService<StorageContext>();
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

            var channel = await _db.Channels.FindAsync(discordChannel.Id);
            if (channel is null) return;

            var webhook = channel.CharacterWebhooks.Find(cw => cw.Id == characterMessage.Author.Id);
            if (webhook is null) return;

            if (reaction.Emote.Name == STOP_BTN.Name)
            {
                webhook.SkipNextBotMessage = true;
                return;
            }

            //if (reaction.Emote.Name == TRANSLATE_BTN.Name)
            //{
            //    _ = TranslateMessageAsync(message, currentChannel.Data.TranslateLanguage);
            //    return;
            //}

            bool userIsLastCaller = webhook.LastDiscordUserCallerId == userReacted.Id;
            bool msgIsSwipable = characterMessage.Id == webhook.LastCharacterDiscordMsgId;
            if (!(userIsLastCaller && msgIsSwipable)) return;

            if (reaction.Emote.Name == ARROW_LEFT.Name && webhook.CurrentSwipeIndex > 0)
            {   // left arrow
                webhook.CurrentSwipeIndex--;
                try { await SwipeMessageAsync(characterMessage, webhook, userReacted); }
                catch(Exception e) { LogException(new[] {e}); }
            }
            else if (reaction.Emote.Name == ARROW_RIGHT.Name)
            {   // right arrow
                webhook.CurrentSwipeIndex++;
                try { await SwipeMessageAsync(characterMessage, webhook, userReacted); }
                catch (Exception e) { LogException(new[] { e }); }
            }
        }

        private async Task HandleButtonAsync(SocketMessageComponent component)
        {
            await component.DeferAsync();

            var searchQuery = _integration.SearchQueries.Find(sq => sq.ChannelId == component.ChannelId);
            if (searchQuery is null || searchQuery.SearchQueryData.IsEmpty) return;
            if (searchQuery.AuthorId != component.User.Id) return;
            if (await UserIsBanned(component.User, _db)) return;

            int tail = searchQuery.SearchQueryData.Characters.Count - (searchQuery.CurrentPage - 1) * 10;
            int maxRow = tail > 10 ? 10 : tail;

            switch (component.Data.CustomId)
            {
                case "up":
                    if (searchQuery.CurrentRow == 1) searchQuery.CurrentRow = maxRow;
                    else searchQuery.CurrentRow--;
                    break;
                case "down":
                    if (searchQuery.CurrentRow > maxRow) searchQuery.CurrentRow = 1;
                    else searchQuery.CurrentRow++;
                    break;
                case "left":
                    searchQuery.CurrentRow = 1;
                    if (searchQuery.CurrentPage == 1) searchQuery.CurrentPage = searchQuery.Pages;
                    else searchQuery.CurrentPage--;
                    break;
                case "right":
                    searchQuery.CurrentRow = 1;
                    if (searchQuery.CurrentPage == searchQuery.Pages) searchQuery.CurrentPage = 1;
                    else searchQuery.CurrentPage++;
                    break;
                case "select":
                    try
                    {
                        await component.Message.ModifyAsync(msg =>
                        {
                            msg.Embed = InlineEmbed(WAIT_MESSAGE, Color.Teal);
                            msg.Components = null;
                        });

                        _integration.SearchQueries.Remove(searchQuery);
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

                        var context = new InteractionContext(_client, component);

                        var webhook = await CreateCharacterWebhookAsync(searchQuery.SearchQueryData.IntegrationType, context, character, _db, _integration);
                        if (webhook is null) return;

                        var webhookClient = new DiscordWebhookClient(webhook.Id, webhook.WebhookToken);
                        _integration.WebhookClients.Add(webhook.Id, webhookClient);

                        await component.Message.ModifyAsync(msg => msg.Embed = SpawnCharacterEmbed(webhook, character));
                        await webhookClient.SendMessageAsync($"{component.User.Mention} {character.Greeting}");
                    }
                    catch (Exception e) { LogException(new[] { e }); }

                    return;
                default:
                    return;
            }

            // Only if left/right/up/down is selected, either this line will never be reached
            await component.Message.ModifyAsync(c => c.Embed = BuildCharactersList(searchQuery)).ConfigureAwait(false);
        }

        private async Task SwipeMessageAsync(IUserMessage characterMessage, CharacterWebhook characterWebhook, SocketGuildUser caller)
        {
            //Drop delay
            if (_integration.RemoveEmojiRequestQueue.ContainsKey(characterMessage.Id))
                _integration.RemoveEmojiRequestQueue[characterMessage.Id] = characterWebhook.Channel.Guild.BtnsRemoveDelay;

            _integration.WebhookClients.TryGetValue(characterWebhook.Id, out DiscordWebhookClient? webhookClient);
            if (webhookClient is null) return;

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
                    characterResponse = new(openAiResponse.Message!, openAiResponse.IsSuccessful, openAiResponse.MessageID, null);
                    characterWebhook.LastRequestTokensUsage = openAiResponse.Usage ?? 0;
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
            
            // Add image to message
            var embeds = new List<Embed>();
            var imageUrl = newCharacterMessage.Value.Value;

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
                await _db.SaveChangesAsync();
            }
            //var tm = TranslatedMessages.Find(tm => tm.MessageId == message.Id);
            //if (tm is not null) tm.IsTranslated = false;
        }

        private static async Task<CharacterResponse> SwipeCaiCharaterResponseAsync(CharacterWebhook characterWebhook, CharacterAIClient? client)
        {
            if (client is null)
            {
                return new($"{WARN_SIGN_DISCORD} CharacterAI integration is disabled", false);
            }

            var caiToken = characterWebhook.Channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;
            if (string.IsNullOrWhiteSpace(caiToken))
            {
                return new($"{WARN_SIGN_DISCORD} You have to specify a CharacterAI auth token for your server first!", false);
            }

            var plusMode = characterWebhook.Channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
            var response = await client.CallCharacterAsync(characterWebhook.Character.Id, characterWebhook.Character.Tgt!, characterWebhook.CaiActiveHistoryId!, parentMsgUuId: characterWebhook.LastUserMsgUuId, customAuthToken: caiToken, customPlusMode: plusMode);
            var text = response.IsSuccessful ? response.Response!.Text : response.ErrorReason!.Replace(WARN_SIGN_UNICODE, WARN_SIGN_DISCORD);

            return new(text, response.IsSuccessful, response.Response?.UuId, response.Response?.ImageRelPath);
        }

        /// <summary>
        /// Called when user presses "select" button in search
        /// </summary>
        private async Task<Models.Database.Character?> SelectCaiCharacterAsync(string characterId, ulong channelId)
        {
            if (_integration.CaiClient is null) return null;

            var channel = await _db.Channels.FindAsync(channelId);
            if (channel is null) return null;

            var caiToken = channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;
            var plusMode = channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
            if (string.IsNullOrWhiteSpace(caiToken)) return null;

            var caiCharacter = await _integration.CaiClient.GetInfoAsync(characterId, customAuthToken: caiToken, customPlusMode: plusMode);
            return CharacterFromCaiCharacterInfo(caiCharacter);
        }
    }
}
