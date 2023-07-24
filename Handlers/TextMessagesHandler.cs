using Discord;
using Discord.Webhook;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;
using CharacterEngineDiscord.Models.Database;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Models.Common;
using System.Net;
using Newtonsoft.Json;
using System.Dynamic;
using Newtonsoft.Json.Linq;
using System.Text;
using CharacterEngineDiscord.Models.OpenAI;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;

namespace CharacterEngineDiscord.Handlers
{
    internal class TextMessagesHandler
    {
        private readonly StorageContext _db;
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationsService _integration;

        public TextMessagesHandler(IServiceProvider services)
        {
            _services = services;
            _db = _services.GetRequiredService<StorageContext>();
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
            var channel = await FindOrStartTrackingChannelAsync(context.Channel.Id, context.Guild.Id, _db);
            if (channel.CharacterWebhooks.Count == 0) return;

            var characterWebhook = await DetermineCalledCharacterWebhook(userMessage, channel, _db);
            if (characterWebhook is null) return;

            try
            {
                if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
                    await TryToCallCaiCharacterAsync(characterWebhook, userMessage);
                else if (characterWebhook.IntegrationType is IntegrationType.OpenAI)
                    await TryToCallOpenAiCharacterAsync(characterWebhook, userMessage);
            }
            catch (Exception e) { LogException(new[] {e}); }
            //bool hasReply = hasMention || message.ReferencedMessage is IUserMessage refm && refm.Author.Id == _client.CurrentUser.Id;

            //bool cnn = currentChannel is not null;
            bool randomReply = false; //cnn && currentChannel!.Data.ReplyChance > (@Random.Next(99) + 0.001 + @Random.NextDouble()); // min: 0 + 0.001 + 0 = 0.001; max: 98 + 0.001 + 1 = 99.001
            bool userIsHunted = false; //cnn && currentChannel!.Data.HuntedUsers.ContainsKey(authorId) && currentChannel.Data.HuntedUsers[authorId] >= @Random.Next(100) + 1;
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

            characterWebhook.AvailableCharacterResponses.Add(characterResponse.Response!.UuId, new(characterResponse.Response.Text, characterResponse.Response.ImageRelPath));
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

            List<Embed>? reference = null;
            if (characterWebhook.ReferencesEnabled && userMessage.ReferencedMessage is not null)
            {
                reference = new() { new EmbedBuilder().WithFooter($"> {userMessage.Content}").Build() };
            }

            var messageId = await webhookClient.SendMessageAsync($"{userMessage.Author.Mention} {characterResponse.Response.Text}", embeds: reference);
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
            var openAiParams = BuildChatOpenAiRequestPayload(characterWebhook);
            var characterResponse = await CallChatOpenAiAsync(openAiParams, _integration.HttpClient);

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

            List<Embed>? reference = null;
            if (characterWebhook.ReferencesEnabled && userMessage.ReferencedMessage is not null)
            {
                int l = userMessage.Content.Length > 30 ? 30 : userMessage.Content.Length;
                reference = new() { new EmbedBuilder().WithFooter($"> {userMessage.Content[0..l]}{(l == 30 ? "...":"")}").Build() };
            }

            var messageId = await webhookClient.SendMessageAsync(text: characterMessage, embeds: reference);
            characterWebhook.LastCharacterDiscordMsgId = messageId;

            await _db.SaveChangesAsync();
            await AddArrowButtonsAsync(messageId, userMessage.Channel, _integration, characterWebhook.Channel.Guild.BtnsRemoveDelay);
        }

        private static async Task<Models.Database.CharacterWebhook?> DetermineCalledCharacterWebhook(SocketUserMessage userMessage, Models.Database.Channel channel, StorageContext db)
        {
            var text = userMessage.Content.Trim();

            var rm = userMessage.ReferencedMessage;
            var withRefMessage = rm is not null && rm.Author.IsWebhook;

            ulong? id;
            if (withRefMessage)
                id = channel.CharacterWebhooks.Find(cw => cw.Id == rm!.Author.Id)?.Id;
            else
                id = channel.CharacterWebhooks.FirstOrDefault(w => text.StartsWith(w.CallPrefix))?.Id;

            if (id is not null) return await db.CharacterWebhooks.FindAsync(id);

            return null;
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
