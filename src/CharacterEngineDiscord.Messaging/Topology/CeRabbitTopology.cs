using CharacterEngineDiscord.Messaging.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CharacterEngineDiscord.Messaging.Topology;

/// <summary>
/// Declares 1 dead-letter exchange + queue (fanout, durable),
/// 1 request direct exchange + queue (durable, dead-lettered),
/// 1 command direct exchange + queue (durable, dead-lettered).
/// Bindings use wildcards so concrete routing keys do not need to be enumerated up front.
/// </summary>
internal sealed class CeRabbitTopology : ICeRabbitTopology
{
    private readonly RabbitMqOptions _options;

    public CeRabbitTopology(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task EnsureCreatedAsync(IChannel channel, CancellationToken cancellationToken)
    {
        // 1. Dead-letter exchange + queue (fanout so anything routed here lands).
        await channel.ExchangeDeclareAsync(
            exchange: _options.DeadLetterExchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _options.DeadLetterQueueName,
            exchange: _options.DeadLetterExchange,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: cancellationToken);

        var dlxArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
        };

        // 2. Requests (Bot -> Server).
        await channel.ExchangeDeclareAsync(
            exchange: _options.RequestExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _options.RequestQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: dlxArgs,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _options.RequestQueueName,
            exchange: _options.RequestExchange,
            routingKey: RoutingKeys.RequestWildcard,
            arguments: null,
            cancellationToken: cancellationToken);

        // 3. Commands (Server -> Bot).
        await channel.ExchangeDeclareAsync(
            exchange: _options.CommandExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _options.CommandQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: dlxArgs,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _options.CommandQueueName,
            exchange: _options.CommandExchange,
            routingKey: RoutingKeys.CommandWildcard,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}
