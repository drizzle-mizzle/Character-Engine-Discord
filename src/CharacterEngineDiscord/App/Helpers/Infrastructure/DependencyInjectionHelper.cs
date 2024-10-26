using CharacterEngine.App.Handlers;
using CharacterEngine.App.SlashCommands.Explicit;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace CharacterEngine.App.Helpers.Infrastructure;


public static class DependencyInjectionHelper
{
    private static ServiceProvider _serviceProvider = null!;

    // Kinda cursed, but it's convenient for events, helpers and background workers
    public static InteractionsHandler GetInteractionsHandler => _serviceProvider.GetRequiredService<InteractionsHandler>();
    public static SlashCommandsHandler GetSlashCommandsHandler => _serviceProvider.GetRequiredService<SlashCommandsHandler>();
    public static ModalsHandler GetModalsHandler => _serviceProvider.GetRequiredService<ModalsHandler>();
    public static ButtonsHandler GetButtonsHandler => _serviceProvider.GetRequiredService<ButtonsHandler>();
    public static MessagesHandler GetMessagesHandler => _serviceProvider.GetRequiredService<MessagesHandler>();
    public static DiscordSocketClient GetDiscordSocketClient => _serviceProvider.GetRequiredService<DiscordSocketClient>();
    public static InteractionService GetInteractionService => _serviceProvider.GetRequiredService<InteractionService>();
    public static ILogger GetLogger => _serviceProvider.GetRequiredService<ILogger>();


    public static ServiceProvider BuildServiceProvider()
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
            services.AddScoped<AppDbContext>(_ => DatabaseHelper.GetDbContext());
            services.AddScoped<SlashCommandsHandler>();
            services.AddScoped<InteractionsHandler>();
            services.AddScoped<ModalsHandler>();
            services.AddScoped<ButtonsHandler>();
            services.AddScoped<MessagesHandler>();
            services.AddScoped<SpecialCommandsHandler>();
        }

        _serviceProvider = services.BuildServiceProvider();
        return _serviceProvider;
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
            AlwaysDownloadDefaultStickers = true
        };

        return new DiscordSocketClient(clientConfig);
    }

}
