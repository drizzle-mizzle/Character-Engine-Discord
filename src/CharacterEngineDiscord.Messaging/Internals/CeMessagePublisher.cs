using CharacterEngineDiscord.Contracts.Abstractions;
using CharacterEngineDiscord.Messaging.Abstractions;
using CharacterEngineDiscord.Messaging.Configuration;
using CharacterEngineDiscord.Messaging.Serialization;
using CharacterEngineDiscord.Messaging.Topology;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CharacterEngineDiscord.Messaging.Internals;

/// <summary>
/// Singleton publisher backed by a single long-lived <see cref="IChannel"/>. RabbitMQ.Client v7
/// channels are safe for concurrent async publishes, so a single channel is sufficient and
/// avoids the per-publish channel-open round trip.
/// </summary>
internal sealed class CeMessagePublisher : ICeMessagePublisher, IAsyncDisposable
{
    private readonly CeRabbitConnection _connection;
    private readonly ICeMessageSerializer _serializer;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<CeMessagePublisher> _logger;
    private readonly SemaphoreSlim _channelLock = new(1, 1);

    private IChannel? _channel;

    public CeMessagePublisher(
        CeRabbitConnection connection,
        ICeMessageSerializer serializer,
        IOptions<RabbitMqOptions> options,
        ILogger<CeMessagePublisher> logger)
    {
        _connection = connection;
        _serializer = serializer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishRequestAsync<TRequest>(TRequest message, CancellationToken cancellationToken = default)
        where TRequest : IRequestMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var channel = await EnsureChannelAsync(cancellationToken);
        var (body, props) = _serializer.Serialize(channel, message);
        var routingKey = RoutingKeys.ForRequest(message.GetType());

        await channel.BasicPublishAsync(
            exchange: _options.RequestExchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogDebug(
            "[{Trace}] Published request {Type} to {Exchange}/{RoutingKey}",
            message.TraceId, message.GetType().Name, _options.RequestExchange, routingKey);
    }

    public async Task PublishCommandAsync<TCommand>(TCommand message, CancellationToken cancellationToken = default)
        where TCommand : ICommandMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var channel = await EnsureChannelAsync(cancellationToken);
        var (body, props) = _serializer.Serialize(channel, message);
        var routingKey = RoutingKeys.ForCommand(message.GetType());

        await channel.BasicPublishAsync(
            exchange: _options.CommandExchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogDebug(
            "[{Trace}] Published command {Type} to {Exchange}/{RoutingKey}",
            message.TraceId, message.GetType().Name, _options.CommandExchange, routingKey);
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel?.IsOpen == true)
        {
            return _channel;
        }

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel?.IsOpen == true)
            {
                return _channel;
            }

            if (_channel is not null)
            {
                try
                {
                    await _channel.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to dispose closed publisher channel");
                }
            }

            var conn = await _connection.GetOrCreateAsync(cancellationToken);
            _channel = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
            return _channel;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        var channel = _channel;
        _channel = null;

        if (channel is not null)
        {
            try
            {
                await channel.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing publisher channel");
            }
        }

        _channelLock.Dispose();
    }
}
