namespace CharacterEngineDiscord.Core.Helpers;

/// <summary>
/// Generator for short correlation identifiers used across logs and reports.
/// </summary>
public static class TraceId
{
    /// <summary>Returns a fresh 8-character lowercase hex trace id.</summary>
    public static string New()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }
}
