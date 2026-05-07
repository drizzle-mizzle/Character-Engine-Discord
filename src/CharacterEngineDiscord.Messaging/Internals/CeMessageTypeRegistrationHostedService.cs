using CharacterEngineDiscord.Messaging.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.Messaging.Internals;

/// <summary>
/// Drains every <see cref="MessageTypeRegistration"/> registered in DI and applies it to
/// the singleton <see cref="ICeMessageSerializer"/> on host start. Registered BEFORE the
/// rabbit infrastructure / consumer hosted services so consumers can resolve concrete CLR
/// types as soon as messages start arriving. Idempotent: callable multiple times safely.
/// </summary>
internal sealed class CeMessageTypeRegistrationHostedService : IHostedService
{
    private readonly ICeMessageSerializer _serializer;
    private readonly IEnumerable<MessageTypeRegistration> _registrations;
    private readonly ILogger<CeMessageTypeRegistrationHostedService> _logger;

    public CeMessageTypeRegistrationHostedService(
        ICeMessageSerializer serializer,
        IEnumerable<MessageTypeRegistration> registrations,
        ILogger<CeMessageTypeRegistrationHostedService> logger)
    {
        _serializer = serializer;
        _registrations = registrations;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var reg in _registrations)
        {
            reg.RegisterTo(_serializer);
            count++;
        }

        _logger.LogInformation("Registered {Count} message type(s) with the serializer", count);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
