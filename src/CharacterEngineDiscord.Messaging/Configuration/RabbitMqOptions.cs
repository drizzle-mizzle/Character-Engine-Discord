using System.ComponentModel.DataAnnotations;

namespace CharacterEngineDiscord.Messaging.Configuration;

/// <summary>
/// Bound from configuration section <c>RabbitMq</c>. Carries broker connection coordinates
/// and the topology names used by <c>CharacterEngineDiscord.Messaging</c>.
/// </summary>
public sealed class RabbitMqOptions
{
    [Required]
    public string Host { get; init; } = "rabbitmq";

    public int Port { get; init; } = 5672;

    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    public string VirtualHost { get; init; } = "/";

    public string RequestExchange { get; init; } = "ce.requests";
    public string CommandExchange { get; init; } = "ce.commands";
    public string DeadLetterExchange { get; init; } = "ce.deadletter";

    public string RequestQueueName { get; init; } = "ce.requests.q";
    public string CommandQueueName { get; init; } = "ce.commands.q";
    public string DeadLetterQueueName { get; init; } = "ce.deadletter.q";

    public ushort PrefetchCount { get; init; } = 8;
    public int RequestedHeartbeatSec { get; init; } = 60;
}
