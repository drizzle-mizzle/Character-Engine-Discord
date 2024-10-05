using CharacterEngine.App.Handlers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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

        // Singleton
        {
            var sakuraAiClient = new SakuraAiClient();
            services.AddSingleton(sakuraAiClient);

            var discordClient = CreateDiscordClient();
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
            services.AddScoped(_ => new AppDbContext(BotConfig.DATABASE_CONNECTION_STRING));
            services.AddScoped<SlashCommandsHandler>();
            services.AddScoped<InteractionsHandler>();
            services.AddScoped<ModalsHandler>();
            services.AddScoped<ButtonsHandler>();
        }

        return services.BuildServiceProvider();
    }


    private static DiscordSocketClient CreateDiscordClient()
    {
        const GatewayIntents intents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.MessageContent | GatewayIntents.GuildWebhooks;

        var clientConfig = new DiscordSocketConfig
        {
            MessageCacheSize = 15,
            GatewayIntents = intents,
            ConnectionTimeout = 20_000,
            DefaultRetryMode = RetryMode.RetryRatelimit,
            AlwaysDownloadDefaultStickers = true,
        };

        return new DiscordSocketClient(clientConfig);
    }
}
