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
        public bool IsFailure { get; }
        public string? ErrorReason { get; }

        public OpenAiChatResponse(HttpResponseMessage response)
        {
            Code = (int)response.StatusCode;
            string? responseContent = response.Content.ReadAsStringAsync().Result;

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    dynamic contentParsed = responseContent.ToDynamicJsonString()!;

                    // Getting character message
                    string? characterMessage = contentParsed.choices?.First?["message"]?["content"];
                    string? characterMessageID = contentParsed.id; // getting stats
                    int? usage = contentParsed.usage?.total_tokens;

                    if (characterMessage is null || characterMessageID is null)
                    {
                        IsSuccessful = false;
                        IsFailure = true;
                        ErrorReason = $"Something went wrong.";
                        return;
                    }

                    Message = characterMessage;
                    MessageId = characterMessageID;
                    Usage = usage;

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
