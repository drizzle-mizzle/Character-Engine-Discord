using CharacterEngineDiscord.Messaging.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CharacterEngineDiscord.Messaging.Internals;

/// <summary>
/// Process-wide singleton owning a single AMQP <see cref="IConnection"/>. Created lazily on the
/// first call to <see cref="GetOrCreateAsync"/>; reopened transparently if the previous one was closed.
/// Disposal closes the connection and the underlying socket.
/// </summary>
internal sealed class CeRabbitConnection : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<CeRabbitConnection> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IConnection? _connection;

    public CeRabbitConnection(IOptions<RabbitMqOptions> options, ILogger<CeRabbitConnection> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IConnection> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        if (_connection?.IsOpen == true)
        {
            return _connection;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsOpen == true)
            {
                return _connection;
            }

            if (_connection is not null)
            {
                try
                {
                    _connection.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to dispose previous closed connection");
                }
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                RequestedHeartbeat = TimeSpan.FromSeconds(_options.RequestedHeartbeatSec),
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _logger.LogInformation(
                "RabbitMQ connection established to {Host}:{Port} (vhost={VHost})",
                _options.Host, _options.Port, _options.VirtualHost);

            return _connection;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        var conn = _connection;
        _connection = null;

        if (conn is null)
        {
            _initLock.Dispose();
            return;
        }

        try
        {
            if (conn.IsOpen)
            {
                await conn.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error closing RabbitMQ connection on dispose");
        }

        try
        {
            conn.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error disposing RabbitMQ connection");
        }

        _initLock.Dispose();
    }
}
