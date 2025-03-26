using System.Text;
using System.Web;
using CharacterEngineDiscord.Modules.Clients.ChubAiClient.Exceptions;
using CharacterEngineDiscord.Modules.Clients.ChubAiClient.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace CharacterEngineDiscord.Modules.Clients.ChubAiClient;


public class ChubAiClient
{
    private readonly HttpClient HTTP_CLIENT;

    private const string URL_BASE = "https://gateway.chub.ai";

    private readonly JsonSerializerSettings _defaultSettings;
    private readonly JsonSerializer _defaultSerializer;


    public ChubAiClient()
    {
        HTTP_CLIENT = new HttpClient();
        HTTP_CLIENT.DefaultRequestHeaders.TryAddWithoutValidation("content-type", "application/json");

        _defaultSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        };

        _defaultSerializer = JsonSerializer.Create(_defaultSettings);

    }


    public enum NsfwMode { noNSFW, allowNSFW, onlyNSFW };
    public async Task<ChubAiCharacter[]> SearchAsync(string query, NsfwMode nsfwMode = NsfwMode.allowNSFW, int maxAmount = 30)
    {
        var url = new StringBuilder($"{URL_BASE}/search?page=1&sort=default&include_forks=true&chub=true");
        url.Append($"&first={maxAmount}&search={HttpUtility.UrlEncode(query)}");

        if (nsfwMode != NsfwMode.noNSFW)
        {
            url.Append("&nsfw=true&nsfl=true");

            if (nsfwMode == NsfwMode.onlyNSFW)
            {
                url.Append("&nsfw_only=true");
            }
        }

        var request = new HttpRequestMessage(HttpMethod.Get, url.ToString());

        var response = await HTTP_CLIENT.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new SearchException(query, response);
        }

        var content = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new SearchException(query, response);
        }

        return JToken.Parse(content)["data"]?["nodes"]?.ToObject<ChubAiCharacter[]>() ?? [];
    }


    public async Task<ChubAiCharacter> GetCharacterInfoAsync(string fullPath)
    {
        var id = fullPath.Trim('/', ' ');
        if (id.Count(c => c == '/') > 1)
        {
            var parts = id.Split('/');
            id = parts[^2] + '/' + parts[^1];
        }

        var url = $"{URL_BASE}/api/characters/{id}?full=true";

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var response = await HTTP_CLIENT.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new GetCharacterInfoException(fullPath, response);
        }

        var content = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new GetCharacterInfoException(fullPath, response);
        }

        return JToken.Parse(content)["node"]!.ToObject<ChubAiCharacter>()!;
    }

}
