using CharacterEngine.App.Handlers;
using CharacterEngine.App.Helpers.Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SakuraAi.Client;

namespace CharacterEngine.App.Helpers.Common;

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


        return services.BuildServiceProvider();
    }
}
