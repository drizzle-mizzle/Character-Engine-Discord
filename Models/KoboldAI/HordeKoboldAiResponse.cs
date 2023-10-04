using CharacterEngineDiscord.Services;
using Newtonsoft.Json.Linq;
using System.Net.Mime;

namespace CharacterEngineDiscord.Models.KoboldAI
{
    public class HordeKoboldAiResponse : IKoboldAiResponse
    {
        public string? MessageId { get; }
        public int Code { get; }
        public bool IsSuccessful { get; }
        public bool IsFailure { get => !IsSuccessful; }
        public string? ErrorReason { get; }

        private string _responseContent = null!;

        public HordeKoboldAiResponse(HttpResponseMessage response)
        {
            Code = (int)response.StatusCode;
            
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    ReadResponseContentAsync(response.Content).Wait();
                    dynamic contentParsed = _responseContent.ToDynamicJsonString()!;

                    string? messageId = contentParsed.id;                    

                    if (messageId is null)
                    {
                        IsSuccessful = false;
                        ErrorReason = $"Something went wrong.";
                        return;
                    }

                    MessageId = messageId;

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
            _responseContent = await content.ReadAsStringAsync();
        }
    }
}
