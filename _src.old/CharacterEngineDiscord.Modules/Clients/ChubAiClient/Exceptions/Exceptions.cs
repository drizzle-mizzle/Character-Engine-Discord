using System.Text;

namespace CharacterEngineDiscord.Modules.Clients.ChubAiClient.Exceptions;


public class SearchException : Exception
{
    private readonly HttpResponseMessage _responseMessage;


    public ValueTask<string> GetResponseDataAsync(HttpResponseMessage response)
        => _responseMessage.GetResponseDataAsync();


    public SearchException(string query, HttpResponseMessage responseMessage)
        : base($"Failed to perform search for query \"{query}\"")
    {
        _responseMessage = responseMessage;
    }
}


public class GetCharacterInfoException : Exception
{
    private readonly HttpResponseMessage _responseMessage;


    public ValueTask<string> GetResponseDataAsync(HttpResponseMessage response)
        => _responseMessage.GetResponseDataAsync();


    public GetCharacterInfoException(string fullPath, HttpResponseMessage responseMessage)
        : base($"Failed to get character info for character \"{fullPath}\"")
    {
        _responseMessage = responseMessage;
    }
}


public static class ExceptionExtensions
{
    public static async ValueTask<string> GetResponseDataAsync(this HttpResponseMessage response)
    {
        var sb = new StringBuilder($"Code: {response.StatusCode:D} {response.StatusCode:G}");

        if (response.ReasonPhrase is string reasonPhrase)
        {
            sb.AppendLine($"Reason: {reasonPhrase}");
        }

        var content = await response.Content.ReadAsStringAsync();

        if (!string.IsNullOrWhiteSpace(content))
        {
            sb.AppendLine($"Content: {content.Replace('\n', ' ')}");
        }

        return sb.ToString();
    }
}

