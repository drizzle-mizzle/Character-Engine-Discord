using CharacterEngineDiscord.Contracts.Abstractions;
using CharacterEngineDiscord.Messaging.Configuration;
using CharacterEngineDiscord.Messaging.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CharacterEngineDiscord.Messaging.Internals;

/// <summary>
/// Background pump that drains the command queue (Server -> Bot direction). Mirrors
/// <see cref="CeRequestConsumerHostedService"/> but resolves <see cref="ICommandMessage"/>
/// payloads against <see cref="CeCommandDispatcher"/>.
/// </summary>
internal sealed class CeCommandConsumerHostedService : BackgroundService
{
    private readonly CeRabbitConnection _connection;
    private readonly ICeMessageSerializer _serializer;
    private readonly CeCommandDispatcher _dispatcher;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<CeCommandConsumerHostedService> _logger;

    private IChannel? _channel;
    private CancellationToken _stoppingToken;

    public CeCommandConsumerHostedService(
        CeRabbitConnection connection,
        ICeMessageSerializer serializer,
        CeCommandDispatcher dispatcher,
        IOptions<RabbitMqOptions> options,
        ILogger<CeCommandConsumerHostedService> logger)
    {
        _connection = connection;
        _serializer = serializer;
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        var conn = await _connection.GetOrCreateAsync(stoppingToken);
        _channel = await conn.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _options.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;

        await _channel.BasicConsumeAsync(
            queue: _options.CommandQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Command consumer attached to {Queue}", _options.CommandQueueName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var channel = _channel;
        if (channel is null)
        {
            return;
        }

        var ct = _stoppingToken;

        try
        {
            var msg = _serializer.Deserialize(ea.Body, ea.BasicProperties);
            if (msg is null)
            {
                _logger.LogError(
                    "Unknown or unregistered command type '{Type}'; rejecting to DLX",
                    ea.BasicProperties.Type ?? "<null>");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct);
                return;
            }

            if (msg is not ICommandMessage cmd)
            {
                _logger.LogError(
                    "Message type '{Type}' arrived on the command queue but does not implement ICommandMessage; rejecting",
                    msg.GetType().Name);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct);
                return;
            }

            // TODO Phase 4: idempotency by MessageEnvelope.MessageId.
            // At-least-once delivery means the same message may be re-delivered
            // (consumer crash before ack, requeue on transient failure, etc.).
            // Add a `processed_messages` table or in-memory LRU cache and short-circuit
            // dispatch here if MessageId was already processed.
            await _dispatcher.DispatchAsync(cmd, ct);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            try
            {
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to nack on shutdown");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Command handler failed for type '{Type}' (delivery {Tag}); requeueing",
                ea.BasicProperties.Type ?? "<null>",
                ea.DeliveryTag);
            try
            {
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, ct);
            }
            catch (Exception nackEx)
            {
                _logger.LogError(nackEx, "Failed to nack delivery {Tag}", ea.DeliveryTag);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

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
                _logger.LogDebug(ex, "Error disposing command consumer channel");
            }
        }
    }
}
