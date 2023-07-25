using CharacterEngineDiscord.Models.Database;
using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Discord.Webhook;

namespace CharacterEngineDiscord.Handlers
{
    internal class ModalsHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly IntegrationsService _integration;
        private readonly StorageContext _db;

        public ModalsHandler(IServiceProvider services)
        {
            _services = services;
            _integration = _services.GetRequiredService<IntegrationsService>();
            _interactions = _services.GetRequiredService<InteractionService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _db = _services.GetRequiredService<StorageContext>();

            _client.ModalSubmitted += (modal) =>
            {
                Task.Run(async () => await HandleModalAsync(modal));
                return Task.CompletedTask;
            };
        }

        internal async Task HandleModalAsync(SocketModal modal)
        {
            await modal.DeferAsync();
            var modalId = modal.Data.CustomId;
            
            // Update call prefix command
            if (modalId.StartsWith("upd"))
            {
                try
                {
                    await UpdateCharacterAsync(modal);
                }
                catch (Exception e)
                {
                    LogException(new[] { e });
                }
            } // Spawn custom character command
            else if (modalId == "spawn")
            {
                try
                {
                    await SpawnCustomCharacterAsync(modal);
                }
                catch (Exception e)
                {
                    LogException(new[] { e });
                }
            }
        }

        private async Task UpdateCharacterAsync(SocketModal modal)
        {
            ulong webhookId = ulong.Parse(modal.Data.CustomId.Split('~').Last());
            var characterWebhook = await _db.CharacterWebhooks.FindAsync(webhookId);

            if (characterWebhook is null)
            {
                await modal.FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Orange));
                return;
            }

            string? newJailbreakPrompt = modal.Data.Components.FirstOrDefault(c => c.CustomId == "new-prompt")?.Value;
            if (string.IsNullOrWhiteSpace(newJailbreakPrompt)) return;

            characterWebhook.UniversalJailbreakPrompt = newJailbreakPrompt;
            await _db.SaveChangesAsync();
            await modal.FollowupAsync(embed: SuccessEmbed());
        }

        private async Task SpawnCustomCharacterAsync(SocketModal modal)
        {
            var channel = await FindOrStartTrackingChannelAsync(modal.Channel.Id, (ulong)modal.GuildId!, _db);
            if (channel is null)
            {
                await modal.FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Channel not found", Color.Orange));
                return;
            }

            string definition = modal.Data.Components.FirstOrDefault(c => c.CustomId == "definition-1")?.Value! +
                               (modal.Data.Components.FirstOrDefault(c => c.CustomId == "definition-2")?.Value ?? "");

            var unsavedCharacter = new Character()
            {
                Id = $"custom-{channel.Id}-{channel.CharacterWebhooks.Count + 1}",
                AuthorName = modal.User.GlobalName,
                Name = modal.Data.Components.FirstOrDefault(c => c.CustomId == "name")?.Value ?? "Unknown",
                Greeting = modal.Data.Components.FirstOrDefault(c => c.CustomId == "first-message")?.Value,
                Definition = definition,
                AvatarUrl = modal.Data.Components.FirstOrDefault(c => c.CustomId == "avatar-url")?.Value,
                Title = "Custom character",
                Description = null,
                ImageGenEnabled = false,
                Interactions = null,
                Stars = null,
                Tgt = null
            };

            var context = new InteractionContext(_client, modal, modal.Channel);
            var characterWebhook = await CreateCharacterWebhookAsync(IntegrationType.Empty, context, unsavedCharacter, _db, _integration);
            if (characterWebhook is null) return;
            
            var webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            _integration.WebhookClients.Add(characterWebhook.Id, webhookClient);

            await modal.FollowupAsync(embed: SpawnCharacterEmbed(characterWebhook));
            await webhookClient.SendMessageAsync($"{modal.User.Mention} {characterWebhook.Character.Greeting}");
        }
    }
}
