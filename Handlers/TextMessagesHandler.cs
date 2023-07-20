using Discord;
using Discord.Webhook;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;
using CharacterEngineDiscord.Models.Database;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.Handlers
{
    internal class TextMessagesHandler
    {
        private readonly StorageContext _db;
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationsService _integration;
        private readonly InteractionService _interactions;

        public TextMessagesHandler(IServiceProvider services)
        {
            _services = services;
            _db = _services.GetRequiredService<StorageContext>();
            _integration = _services.GetRequiredService<IntegrationsService>();
            _interactions = _services.GetRequiredService<InteractionService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.MessageReceived += (message) =>
            {
                Task.Run(async () => await HandleMessageAsync(message));
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
            var channel = await FindOrStartTrackingChannelAsync(context.Channel.Id, context.Guild.Id, _db);
            if (channel.CharacterWebhooks.Count == 0) return;

            var characterWebhook = DetermineCalledCharacterWebhook(userMessage, channel);
            if (characterWebhook is null) return;

            // delete prefix
            var text = userMessage.Content.Trim();
            if (text.StartsWith(characterWebhook.CallPrefix))
                text = text.Remove(0, characterWebhook.CallPrefix.Length);
            try
            {
                switch (characterWebhook.IntegrationType)
                {
                    case IntegrationType.CharacterAI:
                        await TryToCallCaiCharacterAsync(text, characterWebhook, userMessage);
                        break;
                    default:
                        return;
                }
            }
            catch (Exception e) { LogException(new[] {e}); }
            //bool hasReply = hasMention || message.ReferencedMessage is IUserMessage refm && refm.Author.Id == _client.CurrentUser.Id;

            //bool cnn = currentChannel is not null;
            bool randomReply = false; //cnn && currentChannel!.Data.ReplyChance > (@Random.Next(99) + 0.001 + @Random.NextDouble()); // min: 0 + 0.001 + 0 = 0.001; max: 98 + 0.001 + 1 = 99.001
            bool userIsHunted = false; //cnn && currentChannel!.Data.HuntedUsers.ContainsKey(authorId) && currentChannel.Data.HuntedUsers[authorId] >= @Random.Next(100) + 1;
        }

        private async Task TryToCallCaiCharacterAsync(string text, CharacterWebhook characterWebhook, SocketUserMessage userMessage)
        {
            if (_integration.CaiClient is null)
            {
                await userMessage.ReplyAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} CharacterAI integration is disabled", Color.Red));
                return;
            }

            var characterResponse = await _integration.CaiClient.CallCharacterAsync(characterWebhook.Character.Id, characterWebhook.Character.Tgt, characterWebhook.ActiveHistoryId, text, primaryMsgUuId: characterWebhook.LastCharacterMsgUuId);
            if (!characterResponse.IsSuccessful)
            {
                await userMessage.ReplyAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Failed to fetch character response", Color.Red));
                return;
            }

            characterWebhook.AvailableCharacterMessages.Add(characterResponse.Response!.UuId, new(characterResponse.Response.Text, characterResponse.Response.ImageRelPath));
            characterWebhook.CurrentSwipeIndex = 0;
            characterWebhook.LastUserMsgUuId = characterResponse.LastUserMsgUuId;
            characterWebhook.LastCharacterMsgUuId = characterResponse.Response.UuId;
            characterWebhook.LastDiscordUserCallerId = userMessage.Author.Id;
            _integration.WebhookClients.TryGetValue(characterWebhook.Id, out DiscordWebhookClient? webhookClient);

            if (webhookClient is null)
            {
                webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
                _integration.WebhookClients.Add(characterWebhook.Id, webhookClient);
            }

            var messageId = await webhookClient.SendMessageAsync($"{userMessage.Author.Mention} {characterResponse.Response.Text}");
            characterWebhook.LastCharacterDiscordMsgId = messageId;

            await AddArrowButtonsAsync(messageId, userMessage.Channel);
        }

        private static Models.Database.CharacterWebhook? DetermineCalledCharacterWebhook(SocketUserMessage userMessage, Models.Database.Channel channel)
        {
            var text = userMessage.Content.Trim();

            var rm = userMessage.ReferencedMessage;
            var isResponseToWebhook = rm is not null && rm.Author.IsWebhook;

            if (isResponseToWebhook)
            {
                return channel.CharacterWebhooks.Find(cw => cw.Id == rm!.Author.Id);
            }
            else
            {
                return channel.CharacterWebhooks.Find(w => text.StartsWith(w.CallPrefix));
            }
        }

        private static async Task AddArrowButtonsAsync(ulong messageId, ISocketMessageChannel channel)
        {
            var message = await channel.GetMessageAsync(messageId);
            await message.AddReactionAsync(ARROW_LEFT);
            await message.AddReactionAsync(ARROW_RIGHT);
        }
    }
}
