using CharacterEngineDiscord.Messaging.Topology;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.Messaging.Internals;

/// <summary>
/// Bootstraps the broker connection and declares the request/command/dead-letter topology
/// before any publisher or consumer hosted service starts. Idempotent — safe to invoke
/// against an already-existing topology.
/// </summary>
internal sealed class CeRabbitInfrastructureHostedService : IHostedService
{
    private readonly CeRabbitConnection _connection;
    private readonly ICeRabbitTopology _topology;
    private readonly ILogger<CeRabbitInfrastructureHostedService> _logger;

    public CeRabbitInfrastructureHostedService(
        CeRabbitConnection connection,
        ICeRabbitTopology topology,
        ILogger<CeRabbitInfrastructureHostedService> logger)
    {
        _connection = connection;
        _topology = topology;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var conn = await _connection.GetOrCreateAsync(cancellationToken);

        await using var channel = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
        await _topology.EnsureCreatedAsync(channel, cancellationToken);

        _logger.LogInformation("RabbitMQ topology declared (exchanges, queues, bindings, DLX)");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Connection lifetime is owned by CeRabbitConnection (DI singleton) — nothing to do here.
        return Task.CompletedTask;
    }
}
