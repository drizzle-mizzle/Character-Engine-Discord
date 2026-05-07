using CharacterEngineDiscord.Contracts.Commands;
using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Core.Abstractions.Logging;
using CharacterEngineDiscord.Core.Configuration;
using CharacterEngineDiscord.DiscordBot.CommandHandlers;
using CharacterEngineDiscord.DiscordBot.EventForwarders;
using CharacterEngineDiscord.DiscordBot.Hosting;
using CharacterEngineDiscord.DiscordBot.Logging;
using CharacterEngineDiscord.DiscordBot.RateLimiting;
using CharacterEngineDiscord.Messaging.Extensions;
using CharacterEngineDiscord.Messaging.Handlers;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.DiscordBot.Extensions;

/// <summary>
/// DI registration entry-point for the Discord-bot hosting layer (the exe).
/// </summary>
public static class HostingServiceCollectionExtensions
{
    /// <summary>
    /// Wires up the Discord client (sharded), bot logging facade, gateway-event handlers,
    /// and the hosted service that owns the connection.
    /// Expects <see cref="CharacterEngineDiscord.Core.Extensions.CoreServiceCollectionExtensions"/> to have bound the options POCOs already.
    /// </summary>
    public static IServiceCollection AddCharacterEngineDiscordBot(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration; // Configuration sections are already bound via AddCharacterEngineCore.

        services.AddSingleton(sp =>
        {
            var d = sp.GetRequiredService<IOptions<DiscordOptions>>().Value;
            return new DiscordSocketConfig
            {
                MessageCacheSize = d.MessageCacheSize,
                GatewayIntents = GatewayIntents.Guilds,
                ConnectionTimeout = d.ConnectionTimeoutMs > 0 ? d.ConnectionTimeoutMs : 30_000,
                DefaultRetryMode = RetryMode.RetryRatelimit,
                AlwaysDownloadDefaultStickers = false,
            };
        });

        services.AddSingleton(sp => new DiscordShardedClient(sp.GetRequiredService<DiscordSocketConfig>()));

        services.AddSingleton<IDiscordLogger, CeDiscordLogger>();
        services.AddSingleton<CeGuildLifecycleEventForwarder>();
        services.AddSingleton<CeSlashCommandEventForwarder>();

        // Followup uses the unauthenticated webhook endpoint, so no DefaultRequestHeaders.
        services.AddHttpClient("ce-discord-followup");
        services.AddScoped<ICeCommandHandler<RespondToInteractionCommand>, RespondToInteractionCommandHandler>();
        services.AddScoped<ICeCommandHandler<ReportLogToAdminChannelCommand>, ReportLogToAdminChannelCommandHandler>();

        // Bot publishes SlashCommandInvokedRequest + Guild lifecycle requests,
        // consumes RespondToInteractionCommand + ReportLogToAdminChannelCommand.
        services.RegisterMessage<SlashCommandInvokedRequest>();
        services.RegisterMessage<RespondToInteractionCommand>();
        services.RegisterMessage<GuildJoinedRequest>();
        services.RegisterMessage<GuildLeftRequest>();
        services.RegisterMessage<ReportLogToAdminChannelCommand>();

        // Per-user rate limiter for Discord interactions. One singleton, two type-mappings:
        // public callers consume the interface, the cleanup hosted service consumes the
        // concrete type so it can call internal EvictIdle.
        services.AddSingleton<CeWatchDog>();
        services.AddSingleton<ICeWatchDog>(sp => sp.GetRequiredService<CeWatchDog>());
        services.AddHostedService<CeWatchDogCleanupHostedService>();

        services.AddHostedService<CeDiscordBotHostedService>();
        services.AddHostedService<CeSlashCommandRegistrarHostedService>();

        return services;
    }
}
