using CharacterEngineDiscord.Messaging.Abstractions;
using CharacterEngineDiscord.Messaging.Configuration;
using CharacterEngineDiscord.Messaging.Internals;
using CharacterEngineDiscord.Messaging.Serialization;
using CharacterEngineDiscord.Messaging.Topology;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.Messaging.Extensions;

/// <summary>
/// DI registration entry-points for the Character Engine messaging stack.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="RabbitMqOptions"/> from configuration section <c>RabbitMq</c>,
    /// registers the connection wrapper, topology declarer, JSON serializer, publisher,
    /// dispatchers, and the infrastructure hosted service that ensures topology at boot.
    /// </summary>
    public static IServiceCollection AddCharacterEngineMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
                .Bind(configuration.GetSection("RabbitMq"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddSingleton<CeRabbitConnection>();
        services.AddSingleton<ICeRabbitTopology, CeRabbitTopology>();
        services.AddSingleton<ICeMessageSerializer, CeJsonMessageSerializer>();
        services.AddSingleton<ICeMessagePublisher, CeMessagePublisher>();
        services.AddSingleton<CeRequestDispatcher>();
        services.AddSingleton<CeCommandDispatcher>();

        services.AddHostedService<CeRabbitInfrastructureHostedService>();

        return services;
    }

    /// <summary>
    /// Adds the request-queue consumer hosted service. Call from the Server process only.
    /// </summary>
    public static IServiceCollection AddRequestConsumer(this IServiceCollection services)
    {
        services.AddHostedService<CeRequestConsumerHostedService>();
        return services;
    }

    /// <summary>
    /// Adds the command-queue consumer hosted service. Call from the Bot process only.
    /// </summary>
    public static IServiceCollection AddCommandConsumer(this IServiceCollection services)
    {
        services.AddHostedService<CeCommandConsumerHostedService>();
        return services;
    }
}
