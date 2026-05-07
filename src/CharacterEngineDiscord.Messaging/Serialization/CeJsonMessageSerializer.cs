using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using CharacterEngineDiscord.Contracts.Abstractions;
using RabbitMQ.Client;

namespace CharacterEngineDiscord.Messaging.Serialization;

/// <summary>
/// <see cref="System.Text.Json"/>-based serializer. Uses the runtime CLR type name as the
/// AMQP <c>type</c> header, which is then resolved against <see cref="Register{T}"/>'d types
/// on the receiving side. Designed for human-readable payloads in the RabbitMQ Management UI.
/// </summary>
internal sealed class CeJsonMessageSerializer : ICeMessageSerializer
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ConcurrentDictionary<string, Type> _typeRegistry = new(StringComparer.Ordinal);

    public void Register<T>() where T : IDomainMessage
    {
        var t = typeof(T);
        _typeRegistry[t.Name] = t;
    }

    public (ReadOnlyMemory<byte> Body, BasicProperties Properties) Serialize<T>(IChannel channel, T message)
        where T : IDomainMessage
    {
        ArgumentNullException.ThrowIfNull(channel);
        return Serialize(message);
    }

    public (ReadOnlyMemory<byte> Body, BasicProperties Properties) Serialize<T>(T message)
        where T : IDomainMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var runtimeType = message.GetType();
        var body = JsonSerializer.SerializeToUtf8Bytes(message, runtimeType, _jsonOptions);

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = message.MessageId.ToString("N"),
            CorrelationId = message.TraceId,
            Timestamp = new AmqpTimestamp(((DateTimeOffset)message.OccurredAt.ToUniversalTime()).ToUnixTimeSeconds()),
            Type = runtimeType.Name,
            Headers = new Dictionary<string, object?>
            {
                ["x-message-version"] = message.MessageVersion,
            },
        };

        return (body, props);
    }

    public IDomainMessage? Deserialize(ReadOnlyMemory<byte> body, IReadOnlyBasicProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var typeName = properties.Type;
        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        if (!_typeRegistry.TryGetValue(typeName, out var clrType))
        {
            return null;
        }

        var deserialized = JsonSerializer.Deserialize(body.Span, clrType, _jsonOptions);
        return deserialized as IDomainMessage;
    }
}
