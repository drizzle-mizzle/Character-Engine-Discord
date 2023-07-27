using Discord;
using Discord.Webhook;
using Discord.Commands;
using Discord.WebSocket;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;
using CharacterEngineDiscord.Models.Database;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Models.Common;
using Discord.Interactions;

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
            var db = _services.GetRequiredService<StorageContext>();
            var channel = await FindOrStartTrackingChannelAsync(context.Channel.Id, context.Guild.Id, db);

            var characterWebhook = await DetermineCalledCharacterWebhook(userMessage, channel, db);
            if (characterWebhook is null) return;

            if (await _integration.UserIsBanned(context, db)) return;

            try
            {
                if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
                    await TryToCallCaiCharacterAsync(characterWebhook, userMessage);
                else if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                    await TryToCallOpenAiCharacterAsync(characterWebhook, userMessage);
                else
                    await userMessage.ReplyAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Set backend API for this integration. Use `update-character` command.", Color.Orange));
            }
            catch (Exception e) { LogException(new[] {e}); }
            //bool hasReply = hasMention || message.ReferencedMessage is IUserMessage refm && refm.Author.Id == _client.CurrentUser.Id;
        }

        private async Task TryToCallCaiCharacterAsync(CharacterWebhook characterWebhook, SocketUserMessage userMessage)
        {
            string text = userMessage.Content.RemovePrefix(characterWebhook.CallPrefix);
            if (userMessage.Author is not SocketGuildUser user) return;

            text = characterWebhook.MessagesFormat.Replace("{{user}}", $"{user.Nickname ?? user.DisplayName ?? user.Username}")
                                                  .Replace("{{msg}}", $"{text}");

            if (_integration.CaiClient is null)
            {
                await userMessage.ReplyAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} CharacterAI integration is not available", Color.Red));
                return;
            }

            var caiToken = characterWebhook.Channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;

            if (string.IsNullOrWhiteSpace(caiToken))
            {
                await userMessage.ReplyAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} You have to specify a CharacterAI auth token for your server first!", Color.Red));
                return;
            }

            var plusMode = characterWebhook.Channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
            var characterResponse = await _integration.CaiClient.CallCharacterAsync(characterWebhook.Character.Id, characterWebhook.Character.Tgt!, characterWebhook.CaiActiveHistoryId!, text, primaryMsgUuId: characterWebhook.LastCharacterMsgUuId, customAuthToken: caiToken, customPlusMode: plusMode);

            if (!characterResponse.IsSuccessful)
            {
                await userMessage.ReplyAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Failed to fetch a character response", Color.Red));
                return;
            }

            // Remember as "first swiped"            
            characterWebhook.CurrentSwipeIndex = 0;
            characterWebhook.LastUserMsgUuId = characterResponse.LastUserMsgUuId;
            characterWebhook.LastCharacterMsgUuId = characterResponse.Response!.UuId;
            characterWebhook.LastDiscordUserCallerId = userMessage.Author.Id;
            characterWebhook.AvailableCharacterResponses.Clear();
            characterWebhook.AvailableCharacterResponses.Add(characterResponse.Response.UuId, new(characterResponse.Response.Text, characterResponse.Response.ImageRelPath));
            _integration.WebhookClients.TryGetValue(characterWebhook.Id, out DiscordWebhookClient? webhookClient);

            if (webhookClient is null)
            {
                webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
                _integration.WebhookClients.Add(characterWebhook.Id, webhookClient);
            }

            List<Embed>? embeds = new();

            if (characterWebhook.ReferencesEnabled)
                embeds.Add(new EmbedBuilder().WithFooter($"> {userMessage.Content}").Build());

            var imageUrl = characterResponse.Response.ImageRelPath;
            if (imageUrl is not null && await TryGetImageAsync(imageUrl, _integration.HttpClient))
                embeds.Add(new EmbedBuilder().WithImageUrl(imageUrl).Build());

            string characterMessage = $"{userMessage.Author.Mention} {characterResponse.Response.Text}";
            if (characterMessage.Length > 2000)
                characterMessage = characterMessage[0..1994] + "[...]";

            var messageId = await webhookClient.SendMessageAsync(characterMessage, embeds: embeds);
            characterWebhook.LastCharacterDiscordMsgId = messageId;

            await AddArrowButtonsAsync(messageId, userMessage.Channel, _integration, characterWebhook.Channel.Guild.BtnsRemoveDelay);
        }

        private async Task TryToCallOpenAiCharacterAsync(CharacterWebhook characterWebhook, SocketUserMessage userMessage)
        {
            string text = userMessage.Content.RemovePrefix(characterWebhook.CallPrefix);
            if (userMessage.Author is not SocketGuildUser user) return;

            text = characterWebhook.MessagesFormat.Replace("{{user}}", $"{user.Nickname ?? user.DisplayName ?? user.Username}")
                                                  .Replace("{{msg}}", $"{text}");

            string? openAiToken = characterWebhook.PersonalOpenAiApiToken ?? characterWebhook.Channel.Guild.GuildOpenAiApiToken ?? ConfigFile.DefaultOpenAiApiToken.Value;
            
            if (string.IsNullOrWhiteSpace(openAiToken))
            {
                await userMessage.ReplyAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} You have to specify an OpenAI API token for your server first!", Color.Red));
                return;
            }

            characterWebhook.OpenAiHistoryMessages.Add(new() { Role = "user", Content = text, CharacterWebhookId = characterWebhook.Id }); // remember user message (will be included in payload)
            var openAiRequestParams = BuildChatOpenAiRequestPayload(characterWebhook, openAiToken);
            var characterResponse = await CallChatOpenAiAsync(openAiRequestParams, _integration.HttpClient);

            if (characterResponse.IsFailure)
            {
                await userMessage.ReplyAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Failed to fetch character response: `{characterResponse.ErrorReason}`", Color.Red));
                return;
            }

            // Remember character message
            characterWebhook.OpenAiHistoryMessages.Add(new() { Role = "assistant", Content = characterResponse.Message!, CharacterWebhookId = characterWebhook.Id });
            characterWebhook.LastRequestTokensUsage = characterResponse.Usage!;

            // Clear old messages, 40-60 is a good balance between response speed and needed context size, also it's usually pretty close to the GPT-3.5 token limit
            if (characterWebhook.OpenAiHistoryMessages.Count > 60)
                characterWebhook.OpenAiHistoryMessages.RemoveRange(0, 20);

            // Remember as "first swiped"
            characterWebhook.CurrentSwipeIndex = 0;
            characterWebhook.LastCharacterMsgUuId = characterResponse.MessageID;
            characterWebhook.LastDiscordUserCallerId = userMessage.Author.Id;
            characterWebhook.AvailableCharacterResponses.Clear();
            characterWebhook.AvailableCharacterResponses.Add(characterResponse.MessageID!, new(characterResponse.Message!, null));

            _integration.WebhookClients.TryGetValue(characterWebhook.Id, out DiscordWebhookClient? webhookClient);
           
            if (webhookClient is null)
            {
                webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
                _integration.WebhookClients.Add(characterWebhook.Id, webhookClient);
            }

            // Reformatting
            string characterMessage = characterResponse.Message!.Replace("{{char}}", $"**{characterWebhook.Character.Name}**")
                                                                .Replace("{{user}}", $"**{user.Nickname ?? user.GlobalName ?? user.Username}**");
            characterMessage = $"{userMessage.Author.Mention} {characterMessage}";
            if (characterMessage.Length > 2000)
                characterMessage = characterMessage[0..1994] + "[...]";

            List<Embed>? reference = null;
            
            if (characterWebhook.ReferencesEnabled)
            {
                int l = userMessage.Content.Length > 30 ? 30 : userMessage.Content.Length;
                reference = new() { new EmbedBuilder().WithFooter($"> {userMessage.Content[0..l]}{(l == 30 ? "...":"")}").Build() };
            }

            var messageId = await webhookClient.SendMessageAsync(characterMessage, embeds: reference);
            characterWebhook.LastCharacterDiscordMsgId = messageId;

            var db = _services.GetRequiredService<StorageContext>();
            await db.SaveChangesAsync();

            await AddArrowButtonsAsync(messageId, userMessage.Channel, _integration, characterWebhook.Channel.Guild.BtnsRemoveDelay);
        }

        private static Random @Random = new();
        private static async Task<Models.Database.CharacterWebhook?> DetermineCalledCharacterWebhook(SocketUserMessage userMessage, Models.Database.Channel channel, StorageContext db)
        {
            if (channel.CharacterWebhooks.Count == 0) return null;
            var text = userMessage.Content.Trim();

            var rm = userMessage.ReferencedMessage;
            var withRefMessage = rm is not null && rm.Author.IsWebhook;

            CharacterWebhook? characterWebhook = null;

            if (withRefMessage) // if called by reply
                characterWebhook = channel.CharacterWebhooks.Find(cw => cw.Id == rm!.Author.Id);
            else // or if called by prefix
                characterWebhook = channel.CharacterWebhooks.FirstOrDefault(w => text.StartsWith(w.CallPrefix));

            if (characterWebhook is null) // if still null, check if "called" by hunted user
            {
                var chance = @Random.Next(99) + 0.001 + @Random.NextDouble();
                var randomChWebhooks = channel.CharacterWebhooks.Where(w => w.HuntedUsers.FirstOrDefault(h => h.Id == userMessage.Author.Id && h.Chance > chance) is not null).ToList();

                if (randomChWebhooks is not null && randomChWebhooks.Count > 0)
                    characterWebhook = randomChWebhooks[@Random.Next(randomChWebhooks.Count)];
            }

            if (characterWebhook is null) // if still null, check if "called" by random
            {
                var chance = @Random.Next(99) + 0.001 + @Random.NextDouble();
                bool respondOnRandom = channel.RandomReplyChance > chance;
                
                if (respondOnRandom)
                    characterWebhook = channel.CharacterWebhooks[@Random.Next(channel.CharacterWebhooks.Count)];
            }

            if (characterWebhook is not null)
                await db.Entry(characterWebhook).ReloadAsync();

            return characterWebhook;
        }

        private static async Task AddArrowButtonsAsync(ulong messageId, ISocketMessageChannel channel, IntegrationsService _integration, int lifespan)
        {
            var message = await channel.GetMessageAsync(messageId);
            await message.AddReactionAsync(ARROW_LEFT);
            await message.AddReactionAsync(ARROW_RIGHT);

            _ = _integration.RemoveButtonsAsync(message, delay: lifespan);
        }
    }
}
