using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    public class GlobalCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly InteractionService _interactions;
        private readonly StorageContext _db;
        public GlobalCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _interactions = services.GetRequiredService<InteractionService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("shutdown", "Shutdown")]
        public async Task ShutdownAsync()
        {
            await RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Shutting down...", Color.Orange));
            try { _integration?.CaiClient?.KillBrowser(); }
            catch (Exception e) { LogException(new[] { "Failed to kill Puppeteer processes.\n", e.ToString() }); }
            Environment.Exit(0);
        }
    }
}
