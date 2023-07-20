using Discord.Interactions;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    /// <summary>
    /// Commands that do not change any data
    /// </summary>
    public class OneShotCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly InteractionService _interactions;
        private readonly StorageContext _db;
        public OneShotCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _interactions = services.GetRequiredService<InteractionService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        //[SlashCommand("help", "Help")]
        //public async Task Help()
        //{
            
        //}

        [SlashCommand("ping", "Ping")]
        public async Task Ping()
        {
            await RespondAsync("PONG");
        }
    }
}
