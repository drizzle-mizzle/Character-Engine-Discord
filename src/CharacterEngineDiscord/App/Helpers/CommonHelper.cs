namespace CharacterEngineDiscord.Helpers.Common;

public static class CommonHelper
{
    public static string NewTraceId() => Guid.NewGuid().ToString().ToLower()[..4];

    public const string COMMAND_SEPARATOR = "~sep~";
}
