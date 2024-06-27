using CharacterEngine.Api;
using CharacterEngine.Database;
using CharacterEngine.Helpers.Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SakuraAi;

namespace CharacterEngine.Helpers.Common;

public static class ServiceCollectionHelper
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();


    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Singleton
        {
            var sakuraAiClient = new SakuraAiClient();
            sakuraAiClient.InitializeAsync().Wait();
            services.AddSingleton(sakuraAiClient);

            var discordClient = DiscordCommonHelper.CreateDiscordClient();
            services.AddSingleton(discordClient);

            var interactionService = new InteractionService(discordClient.Rest);
            interactionService.InteractionExecuted += InteractionsHandler.HandleInteractionAsync;
            services.AddSingleton(interactionService);

            var logger = LogManager.GetCurrentClassLogger();
            services.AddSingleton<ILogger>(logger);
        }

        // Scoped
        {
            services.AddDbContext<AppDbContext>();
        }

        // Transient
        {
            services.AddTransient<SlashCommandsHandler>();
            services.AddTransient<InteractionsHandler>();
            services.AddTransient<ModalsHandler>();
        }

        return services.BuildServiceProvider();
    }
}
