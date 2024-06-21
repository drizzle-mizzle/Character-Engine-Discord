namespace CharacterEngine.Helpers.Common;

public static class CommonHelper
{
    public static string NewTraceId() => Guid.NewGuid().ToString().ToLower()[..4];

}
