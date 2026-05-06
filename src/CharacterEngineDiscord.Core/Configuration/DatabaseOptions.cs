namespace CharacterEngineDiscord.Core.Configuration;

/// <summary>
/// PostgreSQL connection-string components.
/// Bound from configuration section <c>Database</c>.
/// </summary>
public sealed class DatabaseOptions
{
    public string Host { get; init; } = "db";

    public int Port { get; init; } = 5432;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Database { get; init; } = string.Empty;
}
