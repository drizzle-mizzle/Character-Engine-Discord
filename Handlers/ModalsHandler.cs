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
using PuppeteerSharp;

namespace CharacterEngineDiscord.Handlers
{
    internal class ModalsHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly StorageContext _db;

        public ModalsHandler(IServiceProvider services)
        {
            _services = services;
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
            ulong webhookId = ulong.Parse(modal.Data.CustomId);
            var characterWebhook = await _db.CharacterWebhooks.FindAsync(webhookId);
            if (characterWebhook is null)
            {
                await modal.FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Orange));
            }
            else
            {
                string? newJailbreakPrompt = modal.Data.Components.FirstOrDefault(c => c.CustomId == "new-prompt")?.Value;
                characterWebhook.UniversalJailbreakPrompt = newJailbreakPrompt;
                await _db.SaveChangesAsync();
                await modal.FollowupAsync(embed: SuccessEmbed());
            }
        }
    }
}
