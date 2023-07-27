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
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Threading.Channels;

namespace CharacterEngineDiscord.Handlers
{
    internal class ModalsHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly IntegrationsService _integration;

        public ModalsHandler(IServiceProvider services)
        {
            _services = services;
            _integration = _services.GetRequiredService<IntegrationsService>();
            _interactions = _services.GetRequiredService<InteractionService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            _client.ModalSubmitted += (modal) =>
            {
                Task.Run(async () => await HandleModalAsync(modal));
                return Task.CompletedTask;
            };
        }

        internal async Task HandleModalAsync(SocketModal modal)
        {
            var db = _services.GetRequiredService<StorageContext>();
            if (await UserIsBannedCheckOnly(modal.User, db)) return;

            await modal.DeferAsync();
            var modalId = modal.Data.CustomId;
            
            // Update call prefix command
            if (modalId.StartsWith("upd"))
            {
                try
                {
                    await UpdateCharacterAsync(modal, db);
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
                    await SpawnCustomCharacterAsync(modal, db);
                }
                catch (Exception e)
                {
                    LogException(new[] { e });
                }
            }
        }

        private static async Task UpdateCharacterAsync(SocketModal modal, StorageContext db)
        {
            ulong webhookId = ulong.Parse(modal.Data.CustomId.Split('~').Last());
            var characterWebhook = await db.CharacterWebhooks.FindAsync(webhookId);

            if (characterWebhook is null)
            {
                await modal.FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Orange));
                return;
            }

            await db.Entry(characterWebhook).ReloadAsync();

            string? newJailbreakPrompt = modal.Data.Components.FirstOrDefault(c => c.CustomId == "new-prompt")?.Value;
            if (string.IsNullOrWhiteSpace(newJailbreakPrompt)) return;

            characterWebhook.UniversalJailbreakPrompt = newJailbreakPrompt;
            await db.SaveChangesAsync();

            await modal.FollowupAsync(embed: SuccessEmbed());
        }

        private async Task SpawnCustomCharacterAsync(SocketModal modal, StorageContext db)
        {
            var channel = await FindOrStartTrackingChannelAsync(modal.Channel.Id, (ulong)modal.GuildId!, db);
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
                Greeting = modal.Data.Components.FirstOrDefault(c => c.CustomId == "first-message")?.Value ?? "",
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
            var characterWebhook = await CreateCharacterWebhookAsync(IntegrationType.Empty, context, unsavedCharacter, db, _integration);
            if (characterWebhook is null) return;
            
            var webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            _integration.WebhookClients.Add(characterWebhook.Id, webhookClient);

            await modal.FollowupAsync(embed: SpawnCharacterEmbed(characterWebhook));
            await webhookClient.SendMessageAsync($"{modal.User.Mention} {characterWebhook.Character.Greeting}");
        }
    }
}
