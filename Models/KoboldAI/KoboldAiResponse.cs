using CharacterEngineDiscord.Services;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;

namespace CharacterEngineDiscord.Models.KoboldAI
{
    public class KoboldAiResponse : IKoboldAiResponse
    {
        public string? Message { get; }
        public int Code { get; }
        public bool IsSuccessful { get; }
        public bool IsFailure { get => !IsSuccessful; }
        public string? ErrorReason { get; }

        private dynamic? _responseContent = null!;

        public KoboldAiResponse(HttpResponseMessage response)
        {
            Code = (int)response.StatusCode;
            
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    ReadResponseContentAsync(response.Content).Wait();

                    // Getting character message
                    var results = (JArray)_responseContent.results;
                    string? characterMessage = (results.First() as dynamic)?.text;

                    if (characterMessage is null)
                    {
                        IsSuccessful = false;
                        ErrorReason = $"Something went wrong.";
                        return;
                    }

                    Message = characterMessage;

                    IsSuccessful = true;
                }
                catch
                {
                    IsSuccessful = false;
                    ErrorReason = $"Failed to parse response.";
                }
            }
            else
            {
                IsSuccessful = false;
                ErrorReason = $"{response.ReasonPhrase}\n{_responseContent}";
            }
        }

        private async Task ReadResponseContentAsync(HttpContent content)
        {
            _responseContent = await content.ReadAsJsonAsync();
        }
    }
}
