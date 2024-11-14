namespace CharacterEngineDiscord.Models.Db;


public class Metric
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required MetricType MetricType { get; init; }
    public required string? EntityId { get; init; }
    public required string? Payload { get; init; }
    public required DateTime CreatedAt { get; init; }
}


public enum MetricType
{
    ApplicationLaunch = 0,
    Error = 1,
    JoinedGuild = 2,
    LeftGuild = 3,
    IntegrationCreated = 4,
    CharacterSpawned = 5,
    CharacterCalled = 6
}
