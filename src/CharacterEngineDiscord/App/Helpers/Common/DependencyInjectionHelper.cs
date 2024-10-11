using CharacterEngine.App.Handlers;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace CharacterEngine.App.Helpers.Common;


public static class DependencyInjectionHelper
{
    private static IServiceProvider _serviceProvider = null!;


    public static IServiceProvider GetServiceProvider => _serviceProvider ??= BuildServiceProvider();
    public static InteractionsHandler GetInteractionsHandler => _serviceProvider.GetRequiredService<InteractionsHandler>();
    public static SlashCommandsHandler GetSlashCommandsHandler => _serviceProvider.GetRequiredService<SlashCommandsHandler>();
    public static ModalsHandler GetModalsHandler => _serviceProvider.GetRequiredService<ModalsHandler>();
    public static ButtonsHandler GetButtonsHandler => _serviceProvider.GetRequiredService<ButtonsHandler>();


    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        // Singleton
        {
            var discordClient = CreateDiscordClient();
            services.AddSingleton(discordClient);

            var interactionService = new InteractionService(discordClient.Rest);
            services.AddSingleton(interactionService);

            var logger = LogManager.GetCurrentClassLogger();
            services.AddSingleton<ILogger>(logger);
        }

        // Scoped
        {
            services.AddScoped(_ => DatabaseHelper.GetDbContext());
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
