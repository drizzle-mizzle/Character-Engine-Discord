using CharacterEngineDiscord.Core.Configuration;
using CharacterEngineDiscord.Server.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.Server.Extensions;

/// <summary>
/// DI registration entry-point for the Server-side composition root.
/// Phase 1 only wires the messaging-template post-configure step; concrete
/// request handlers will be registered here in later phases.
/// </summary>
public static class ServerServiceCollectionExtensions
{
    public static IServiceCollection AddCharacterEngineServer(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration; // Sections already bound via AddCharacterEngineCore.

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<MessagesOptions>, MessagesOptionsPostConfigure>());

        return services;
    }
}
