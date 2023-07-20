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

namespace CharacterEngineDiscord.Handlers
{
    internal class ButtonsAndReactionsHandler
    {
        private readonly StorageContext _db;
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationsService _integration;
        private readonly InteractionService _interactions;

        public ButtonsAndReactionsHandler(IServiceProvider services)
        {
            _services = services;
            _db = _services.GetRequiredService<StorageContext>();
            _integration = _services.GetRequiredService<IntegrationsService>();
            _interactions = _services.GetRequiredService<InteractionService>();
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

            var userReacted = (SocketUser)user;
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
                try { await SwipeMessageAsync(characterMessage, webhook); }
                catch(Exception e) { LogException(new[] {e}); }
            }
            else if (reaction.Emote.Name == ARROW_RIGHT.Name)
            {   // right arrow
                webhook.CurrentSwipeIndex++;
                try { await SwipeMessageAsync(characterMessage, webhook); }
                catch (Exception e) { LogException(new[] { e }); }
            }
        }

        private async Task SwipeMessageAsync(IUserMessage characterMessage, CharacterWebhook characterWebhook)
        {
            //Drop delay
            //if (RemoveEmojiRequestQueue.ContainsKey(message.Id))
            //RemoveEmojiRequestQueue[message.Id] = BotConfig.BtnsRemoveDelay;

            _integration.WebhookClients.TryGetValue(characterWebhook.Id, out DiscordWebhookClient? webhookClient);
            if (webhookClient is null) return;

            // Check if fetching a new message, or just swiping among already available ones
            if (characterWebhook.AvailableCharacterMessages.Count < characterWebhook.CurrentSwipeIndex + 1)
            {   // fetch new
                await webhookClient.ModifyMessageAsync(characterMessage.Id, msg =>
                {
                    msg.Content = null;
                    msg.Embeds = new List<Embed> { InlineEmbed(WAIT_MESSAGE, Color.Teal) };
                    msg.AllowedMentions = AllowedMentions.None;
                });

                Models.CharacterResponse characterResponse;
                switch (characterWebhook.IntegrationType)
                {
                    case IntegrationType.CharacterAI:
                        characterResponse = await GetCaiCharacterResponseAsync(characterWebhook, _integration.CaiClient);
                        break;
                    default:
                        return;
                }
                
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
                characterWebhook.AvailableCharacterMessages.Add(characterResponse.CharacterMessageId!, new(characterResponse.Text, characterResponse.ImageRelPath));
            }

            var newCharacterMessage = characterWebhook.AvailableCharacterMessages.ElementAt(characterWebhook.CurrentSwipeIndex);
            characterWebhook.LastCharacterMsgUuId = newCharacterMessage.Key;

            // Add image to the message
            var embeds = new List<Embed>();
            var imageUrl = newCharacterMessage.Value.Value;

            if (imageUrl is not null && await TryGetImageAsync(imageUrl, _integration.HttpClient))
                embeds.Add(new EmbedBuilder().WithImageUrl(imageUrl).Build());

            // Add text to the message
            string responseText = newCharacterMessage.Value.Key;
            if (responseText.Length > 2000)
                responseText = responseText[0..1994] + "[...]";

            // Send (update) message
            await webhookClient.ModifyMessageAsync(characterMessage.Id, msg =>
            {
                msg.Content = $"{responseText}";
                msg.Embeds = embeds;
                msg.AllowedMentions = AllowedMentions.All;
            });
            //var tm = TranslatedMessages.Find(tm => tm.MessageId == message.Id);
            //if (tm is not null) tm.IsTranslated = false;
        }

        private static async Task<Models.CharacterResponse> GetCaiCharacterResponseAsync(CharacterWebhook characterWebhook, CharacterAIClient? client)
        {
            if (client is null)
            {
                return new($"{WARN_SIGN_DISCORD} CharacterAI integration is disabled", false);
            }

            var response = await client.CallCharacterAsync(characterWebhook.Character.Id, characterWebhook.Character.Tgt, characterWebhook.ActiveHistoryId, parentMsgUuId: characterWebhook.LastUserMsgUuId);
            var text = response.IsSuccessful ? response.Response!.Text : response.ErrorReason!.Replace(WARN_SIGN_UNICODE, WARN_SIGN_DISCORD);

            return new(text, response.IsSuccessful, response.Response?.UuId, response.Response?.ImageRelPath);
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
                    await component.Message.ModifyAsync(msg =>
                    {
                        msg.Embed = InlineEmbed(WAIT_MESSAGE, Color.Teal);
                        msg.Components = null;
                    });

                    _integration.SearchQueries.Remove(searchQuery);
                    int index = (searchQuery.CurrentPage - 1) * 10 + searchQuery.CurrentRow - 1;
                    string characterId = searchQuery.SearchQueryData.Characters[index].Id;

                    Models.Database.Character? character;
                    switch (searchQuery.IntegrationType)
                    {
                        case IntegrationType.CharacterAI:
                            character = await SelectCaiCharacterAsync(characterId);
                            break;
                        default:
                            return;
                    }

                    if (character is null)
                    {
                        await component.Message.ModifyAsync(msg => msg.Embed = FailedToSetCharacterEmbed());
                        return;
                    }

                    var context = new InteractionContext(_client, component);
                    
                    var webhook = await CreateChannelCharacterWebhookAsync(searchQuery.IntegrationType, context, character, _db, _integration);
                    if (webhook is null) return;

                    var webhookClient = new DiscordWebhookClient(webhook.Id, webhook.WebhookToken);
                    _integration.WebhookClients.Add(webhook.Id, webhookClient);

                    await component.Message.ModifyAsync(msg => msg.Embed = SpawnCharacterEmbed(webhook, character));
                    await webhookClient.SendMessageAsync($"{component.User.Mention} {character.Greeting}");

                    return;
                default:
                    return;
            }

            // Only if left/right/up/down is selected, either this line will never be reached
            await component.Message.ModifyAsync(c => c.Embed = BuildCharactersList(searchQuery)).ConfigureAwait(false);
        }

        private async Task<Models.Database.Character?> SelectCaiCharacterAsync(string characterId)
        {
            if (_integration.CaiClient is null) return null;

            var caiCharacter = await _integration.CaiClient.GetInfoAsync(characterId);
            return CharacterFromCaiCharacterInfo(caiCharacter);
        }
    }
}
