using CharacterEngineDiscord.Services;

namespace CharacterEngineDiscord.Models.OpenAI
{
    public class OpenAiChatResponse : IOpenAiResponse
    {
        public string? Message { get; }
        public string? MessageId { get; }
        public int? Usage { get; }
        public int Code { get; }
        public bool IsSuccessful { get; }
        public bool IsFailure { get => !IsSuccessful; }
        public string? ErrorReason { get; }

        private dynamic? _responseContent = null!;

        public OpenAiChatResponse(HttpResponseMessage response)
        {
            Code = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    ReadResponseContentAsync(response.Content).Wait();

                    // Getting character message
                    string? characterMessage = _responseContent.choices?.First?["message"]?["content"];
                    string? characterMessageID = _responseContent.id; // getting stats
                    int? usage = _responseContent.usage?.total_tokens;

                    if (characterMessage is null || characterMessageID is null)
                    {
                        IsSuccessful = false;
                        ErrorReason = $"Something went wrong.";
                        return;
                    }

                    Message = characterMessage;
                    MessageId = characterMessageID;
                    Usage = usage;

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
