using CharacterEngine.App;
using CharacterEngine.App.Handlers;
using CharacterEngine.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SakuraAi;

namespace CharacterEngine.Helpers.Common;

public static class CommonHelper
{
    public static string NewTraceId() => Guid.NewGuid().ToString().ToLower()[..4];


    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Transient
        {
            services.AddTransient<SlashCommandsHandler>();
            services.AddTransient<InteractionsHandler>();
            services.AddTransient<ModalsHandler>();
        }

        // Singleton
        {
            var sakuraAiClient = new SakuraAiClient();
            services.AddSingleton(sakuraAiClient);

            var discordClient = DiscordLaunchHelper.CreateDiscordClient();
            services.AddSingleton(discordClient);

            var interactionService = new InteractionService(discordClient.Rest);
            services.AddSingleton(interactionService);

            var logger = LogManager.GetCurrentClassLogger();
            services.AddSingleton<ILogger>(logger);

            var localStorage = new LocalStorage();
            services.AddSingleton(localStorage);
        }

        // Scoped
        {
            services.AddDbContext<AppDbContext>();
        }


        return services.BuildServiceProvider();
    }
}
