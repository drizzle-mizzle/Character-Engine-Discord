using CharacterEngineDiscord.Services;
using Newtonsoft.Json.Linq;
using System.Net.Mime;

namespace CharacterEngineDiscord.Models.KoboldAI
{
    public class HordeKoboldAiResponse : IKoboldAiResponse
    {
        public string? Id { get; }
        public int Code { get; }
        public bool IsSuccessful { get; }
        public bool IsFailure { get => !IsSuccessful; }
        public string? ErrorReason { get; }

        private dynamic? _responseContent = null!;

        public HordeKoboldAiResponse(HttpResponseMessage response)
        {
            Code = (int)response.StatusCode;
            
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    ReadResponseContentAsync(response.Content).Wait();

                    string? id = _responseContent.id;                    

                    if (id is null)
                    {
                        IsSuccessful = false;
                        ErrorReason = $"Something went wrong.";
                        return;
                    }

                    Id = id;

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
