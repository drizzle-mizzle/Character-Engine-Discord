using Microsoft.Extensions.Primitives;
using System.Runtime.CompilerServices;

namespace CharacterEngineDiscord.Models.OpenAI
{
    public class OpenAiChatRequestParams
    {
        public required string ApiEndpoint { get; set; }
        public required string ApiToken { get; set; }
        public required string Model { get; set; }
        public float FreqPenalty { get; set; }
        public float PresencePenalty { get; set; }
        public float Temperature { get; set; }
        public int MaxTokens { get; set; }
        public string? UniversalJailbreakPrompt { get; set; }
        public required List<OpenAiMessage> Messages { get; set; }
    }

    public readonly struct OpenAiMessage
    {
        public string Role { get; }
        public string Content { get; }

        public OpenAiMessage(string role, string content)
        {
            Content = content;
            Role = role;
        }
    }
}
