using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.Eventing.Reader;

namespace CharacterEngineDiscord.Models.CharacterHub
{
    public class ChubSearchResponse : IChubResponse
    {
        public List<ChubCharacter> Characters { get; }
        public bool IsEmpty { get => Characters.Count == 0; }
        public int Amount { get; }
        public int CurrentPage { get; }
        public int Code { get; }
        public bool IsSuccessful { get; }
        public bool IsFailure { get; }
        public string? ErrorReason { get; } = null;
        public string OriginalQuery { get; }

        public ChubSearchResponse(HttpResponseMessage response, string originalQuery)
        {
            OriginalQuery = originalQuery;
            Characters = new();
            Code = (int)response.StatusCode;
            string responseContent = response.Content.ReadAsStringAsync().Result;

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var responseParsed = JsonConvert.DeserializeObject<dynamic>(responseContent)!.data;
                    Amount = (int)responseParsed.count;
                    CurrentPage = (int)responseParsed.page;
                    Characters = new();

                    JArray nodes = responseParsed.nodes;
                    foreach (dynamic node in nodes)
                        Characters.Add(new(node, false));

                    IsSuccessful = true;
                    IsFailure = false;
                }
                catch
                {
                    IsSuccessful = false;
                    IsFailure = true;
                    ErrorReason = $"Failed to parse response.";
                }
            }
            else
            {
                IsSuccessful = false;
                IsFailure = true;
                ErrorReason = $"{response.ReasonPhrase}\n{responseContent}";
            }
        }
    }
}
