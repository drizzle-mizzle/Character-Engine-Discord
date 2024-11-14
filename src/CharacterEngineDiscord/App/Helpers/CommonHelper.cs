using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;

namespace CharacterEngine.App.Helpers;

public static class CommonHelper
{
    public static string NewTraceId() => Guid.NewGuid().ToString().ToLower()[..6];


    public static async Task<Stream?> DownloadFileAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            return await MemoryStorage.CommonHttpClient.GetStreamAsync(url);
        }
        catch (Exception e)
        {
            await CharacterEngineBot.DiscordShardedClient.ReportErrorAsync(e, NewTraceId(), false);
            return null;
        }
    }
}
