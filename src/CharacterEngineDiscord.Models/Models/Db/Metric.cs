namespace CharacterEngineDiscord.Models.Db;


public class Metric
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required MetricType MetricType { get; init; }
    public required string EntityId { get; init; }
    public required string Payload { get; init; }
    public required DateTime CreatedAt { get; init; }
}


public enum MetricType
{
    JoinedGuild,
    LeftGuild,
    Interaction,
    CharacterSpawned,
    CharacterCalled
}


public enum MetricBase
{
    User,
    Guild
}
