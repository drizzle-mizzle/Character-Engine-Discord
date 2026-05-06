using CharacterEngineDiscord.Core.Abstractions.Logging;
using CharacterEngineDiscord.Core.Configuration;
using CharacterEngineDiscord.DiscordBot.Hosting;
using CharacterEngineDiscord.DiscordBot.Logging;
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

        services.AddSingleton<IDiscordLogger, DiscordLogger>();
        services.AddSingleton<GuildLifecycleHandler>();
        services.AddHostedService<CeDiscordBotHostedService>();

        return services;
    }
}
