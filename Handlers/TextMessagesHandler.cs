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
using System.Data.Entity.ModelConfiguration.Conventions;

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

            try
            {
                // Get stored channel and its' characters
                var characterWebhooks = await DetermineCalledCharacterWebhook(userMessage, context.Channel.Id);
                if (characterWebhooks.Count == 0) return;
                if (await _integration.UserIsBanned(context)) return;

                foreach (var characterWebhook in characterWebhooks)
                {
                    if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
                        await TryToCallCaiCharacterAsync(characterWebhook.Id, userMessage);
                    else if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                        await TryToCallOpenAiCharacterAsync(characterWebhook.Id, userMessage);
                    else
                        await userMessage.ReplyAsync(embed: $"{WARN_SIGN_DISCORD} Set backend API for this integration. Use `update-character` command.".ToInlineEmbed(Color.Orange));
                }
            }
            catch (Exception e) { LogException(new[] {e}); }
        }

        private async Task TryToCallCaiCharacterAsync(ulong characterWebhookId, SocketUserMessage userMessage)
        {
            var db = new StorageContext();
            var characterWebhook = await db.CharacterWebhooks.FindAsync(characterWebhookId);
            if (characterWebhook is null) return;

            string text = userMessage.Content.RemovePrefix(characterWebhook.CallPrefix);
            if (userMessage.Author is not SocketGuildUser user) return;

            text = characterWebhook.MessagesFormat.Replace("{{user}}", $"{user.Nickname ?? user.DisplayName ?? user.Username}")
                                                  .Replace("{{msg}}", $"{text}");

            if (_integration.CaiClient is null)
            {
                await userMessage.ReplyAsync(embed: $"{WARN_SIGN_DISCORD} CharacterAI integration is not available".ToInlineEmbed(Color.Red));
                return;
            }

            var caiToken = characterWebhook.Channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;

            if (string.IsNullOrWhiteSpace(caiToken))
            {
                await userMessage.ReplyAsync(embed: $"{WARN_SIGN_DISCORD} You have to specify a CharacterAI auth token for your server first!".ToInlineEmbed(Color.Red));
                return;
            }

            var plusMode = characterWebhook.Channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
            var characterResponse = await _integration.CaiClient.CallCharacterAsync(characterWebhook.Character.Id, characterWebhook.Character.Tgt!, characterWebhook.CaiActiveHistoryId!, text, primaryMsgUuId: characterWebhook.LastCharacterMsgUuId, customAuthToken: caiToken, customPlusMode: plusMode);

            if (!characterResponse.IsSuccessful)
            {
                await userMessage.ReplyAsync(embed: $"{WARN_SIGN_DISCORD} Failed to fetch a character response".ToInlineEmbed(Color.Red));
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

        private async Task TryToCallOpenAiCharacterAsync(ulong characterWebhookId, SocketUserMessage userMessage)
        {
            var db = new StorageContext();
            var characterWebhook = await db.CharacterWebhooks.FindAsync(characterWebhookId);
            if (characterWebhook is null) return;

            string text = userMessage.Content.RemovePrefix(characterWebhook.CallPrefix);
            if (userMessage.Author is not SocketGuildUser user) return;

            text = characterWebhook.MessagesFormat.Replace("{{user}}", $"{user.Nickname ?? user.DisplayName ?? user.Username}")
                                                  .Replace("{{msg}}", $"{text}");

            string? openAiToken = characterWebhook.PersonalOpenAiApiToken ?? characterWebhook.Channel.Guild.GuildOpenAiApiToken ?? ConfigFile.DefaultOpenAiApiToken.Value;
            
            if (string.IsNullOrWhiteSpace(openAiToken))
            {
                await userMessage.ReplyAsync(embed: $"{WARN_SIGN_DISCORD} You have to specify an OpenAI API token for your server first!".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.OpenAiHistoryMessages.Add(new() { Role = "user", Content = text, CharacterWebhookId = characterWebhook.Id }); // remember user message (will be included in payload)
            var openAiRequestParams = BuildChatOpenAiRequestPayload(characterWebhook, openAiToken);
            var characterResponse = await CallChatOpenAiAsync(openAiRequestParams, _integration.HttpClient);

            if (characterResponse.IsFailure)
            {
                await userMessage.ReplyAsync(embed: $"{WARN_SIGN_DISCORD} Failed to fetch character response: `{characterResponse.ErrorReason}`".ToInlineEmbed(Color.Red));
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
            
            await db.SaveChangesAsync();

            await AddArrowButtonsAsync(messageId, userMessage.Channel, _integration, characterWebhook.Channel.Guild.BtnsRemoveDelay);
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

        private static async Task AddArrowButtonsAsync(ulong messageId, ISocketMessageChannel channel, IntegrationsService _integration, int lifespan)
        {
            var message = await channel.GetMessageAsync(messageId);
            await message.AddReactionAsync(ARROW_LEFT);
            await message.AddReactionAsync(ARROW_RIGHT);

            _ = Task.Run(() => _integration.RemoveButtonsAsync(message, delay: lifespan));
        }
    }
}
