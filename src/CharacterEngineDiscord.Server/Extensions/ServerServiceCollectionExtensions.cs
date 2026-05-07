using CharacterEngineDiscord.Contracts.Commands;
using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Core.Abstractions.Logging;
using CharacterEngineDiscord.Core.Configuration;
using CharacterEngineDiscord.Messaging.Extensions;
using CharacterEngineDiscord.Messaging.Handlers;
using CharacterEngineDiscord.Server.Configuration;
using CharacterEngineDiscord.Server.Logging;
using CharacterEngineDiscord.Server.RequestHandlers;
using CharacterEngineDiscord.Server.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.Server.Extensions;

/// <summary>
/// DI registration entry-point for the Server-side composition root.
/// Wires the messaging-template post-configure step, the Server-side
/// <see cref="IDiscordLogger"/> (publishes admin-channel notifications onto the
/// command bus), the slash-command router, the per-command handlers, and the
/// Guild-lifecycle request handlers.
/// </summary>
public static class ServerServiceCollectionExtensions
{
    public static IServiceCollection AddCharacterEngineServer(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration; // Sections already bound via AddCharacterEngineCore.

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<MessagesOptions>, MessagesOptionsPostConfigure>());

        services.AddSingleton<IDiscordLogger, CeDiscordLogger>();

        // Server consumes SlashCommandInvokedRequest + Guild lifecycle requests,
        // publishes RespondToInteractionCommand + ReportLogToAdminChannelCommand.
        services.RegisterMessage<SlashCommandInvokedRequest>();
        services.RegisterMessage<RespondToInteractionCommand>();
        services.RegisterMessage<GuildJoinedRequest>();
        services.RegisterMessage<GuildLeftRequest>();
        services.RegisterMessage<ReportLogToAdminChannelCommand>();

        // Routing + per-request handlers.
        services.AddScoped<PingSlashCommandHandler>();
        services.AddScoped<ICeRequestHandler<SlashCommandInvokedRequest>, CeSlashCommandRouter>();
        services.AddScoped<ICeRequestHandler<GuildJoinedRequest>, GuildJoinedRequestHandler>();
        services.AddScoped<ICeRequestHandler<GuildLeftRequest>, GuildLeftRequestHandler>();

        return services;
    }
}
