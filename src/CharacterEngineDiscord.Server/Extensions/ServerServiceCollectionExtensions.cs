using CharacterEngineDiscord.Contracts.Commands;
using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Core.Configuration;
using CharacterEngineDiscord.Messaging.Extensions;
using CharacterEngineDiscord.Messaging.Handlers;
using CharacterEngineDiscord.Server.Configuration;
using CharacterEngineDiscord.Server.RequestHandlers;
using CharacterEngineDiscord.Server.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.Server.Extensions;

/// <summary>
/// DI registration entry-point for the Server-side composition root.
/// Wires the messaging-template post-configure step plus the slash-command
/// router and per-command handlers that consume <see cref="SlashCommandInvokedRequest"/>.
/// </summary>
public static class ServerServiceCollectionExtensions
{
    public static IServiceCollection AddCharacterEngineServer(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration; // Sections already bound via AddCharacterEngineCore.

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<MessagesOptions>, MessagesOptionsPostConfigure>());

        // Server consumes SlashCommandInvokedRequest and publishes RespondToInteractionCommand.
        services.RegisterMessage<SlashCommandInvokedRequest>();
        services.RegisterMessage<RespondToInteractionCommand>();

        // Routing + per-command handlers.
        services.AddScoped<PingSlashCommandHandler>();
        services.AddScoped<ICeRequestHandler<SlashCommandInvokedRequest>, CeSlashCommandRouter>();

        return services;
    }
}
