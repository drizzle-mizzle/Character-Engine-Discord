using CharacterAi.Client.Exceptions;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Modules;
using Discord.Net;
using SakuraAi.Client.Exceptions;

namespace CharacterEngine.App.Helpers;

public static class CommonHelper
{
    public const string COMMAND_SEPARATOR = "~sep~";
    
    public static string NewTraceId() => Guid.NewGuid().ToString().ToLower()[..6];
    public static HttpClient CommonHttpClient { get; } = new() { MaxResponseContentBufferSize = 5_242_880 };


    public static async Task<Stream?> DownloadFileAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            return await CommonHttpClient.GetStreamAsync(url);
        }
        catch (Exception e)
        {
            await CharacterEngineBot.DiscordClient.ReportErrorAsync($"DownloadFileAsync: {url}", null, $"URL: {url}\nException:\n{e}", NewTraceId(), false);
            return null;
        }
    }


    public static (bool Pass, string? Message) ValidateUserFriendlyException(this Exception exception)
    {
        var ie = exception.InnerException;
        if (ie is not null && Check(ie))
        {
            return (true, ie.Message);
        }

        return Check(exception) ? (true, exception.Message) : (false, null);

        bool Check(Exception e)
            => e is UserFriendlyException or ChatModuleException or SakuraException or CharacterAiException or HttpRequestException;
    }


    public static (bool Pass, string? Message) ValidateWebhookException(this Exception exception)
    {
        var ie = exception.InnerException;
        if (ie is not null && Check(ie))
        {
            return (true, ie.Message);
        }

        return Check(exception) ? (true, exception.Message) : (false, null);

        bool Check(Exception e)
            => (e is HttpException or InvalidOperationException)
            && (e.Message.Contains("Unknown Webhook") || e.Message.Contains("Could not find a webhook"));
    }
}
