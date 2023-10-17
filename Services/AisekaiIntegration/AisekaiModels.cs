using System.ComponentModel;

namespace CharacterEngineDiscord.Services.AisekaiIntegration.Models
{
    public struct Character
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required string Author { get; set; }
        public required string? AvatarUrl { get; set; }
        public required ulong MessageCount { get; set; }
        public required ulong ChatCount { get; set; }
        public required ulong LikeCount { get; set; }
        public required bool NSFW { get; set; }
        public required string Visibility { get; set; }
        public required IEnumerable<string>? Tags { get; set; }
        public required DateTime UpdatedAt { get; set; }
        public required DateTime CreatedAt { get; set; }
    }

    public struct LoginResponse
    {
        public string? ExpToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? Message { get; set; }
        public required bool IsSuccessful { get; set; }
    }

    public struct CharacterResponse
    {
        public required string LastMessageId { get; set; }
        public required string Content { get; set; }
    }

    public struct ResetResponse : IAisekaiResponse
    {
        public string? Greeting { get; set; }
        public required int Code { get; set; }
        public required bool IsSuccessful { get; set; }
        public string? ErrorReason { get; set; }
    }

    public struct ChatInfoResponse : IAisekaiResponse
    {
        public string? ChatId { get; set; }
        public string? GreetingMessage { get; set; }
        public bool? InitiatorEngineEnabled { get; set; }
        // Messages ...
        public required int Code { get; set; }
        public required bool IsSuccessful { get; set; }
        public string? ErrorReason { get; set; }
    }

    public struct CharacterInfoResponse : IAisekaiResponse
    {
        public Character? Character { get; set; }
        public required int Code { get; set; }
        public required bool IsSuccessful { get; set; }
        public string? ErrorReason { get; set; }
    }

    public struct SwipeResponse : IAisekaiResponse
    {
        public string? Content { get; set; }
        public required int Code { get; set; }
        public required bool IsSuccessful { get; set; }
        public string? ErrorReason { get; set; }
    }

    public struct CallResponse : IAisekaiResponse
    {
        public CharacterResponse? CharacterResponse { get; set; }
        public required int Code { get; set; }
        public required bool IsSuccessful { get; set; }
        public string? ErrorReason { get; set; }
    }

    public struct EditResponse : IAisekaiResponse
    {
        public required int Code { get; set; }
        public required bool IsSuccessful { get; set; }
        public string? ErrorReason { get; set; }
    }

    public struct SearchResponse : IAisekaiResponse
    {
        public required List<Character> Characters { get; set; }
        public required string OriginalQuery { get; set; }
        public required int Code { get; set; }
        public required bool IsSuccessful { get; set; }
        public string? ErrorReason { get; set; }
    }

    public interface IAisekaiResponse
    {
        int Code { get; }
        string? ErrorReason { get; }
        public bool IsSuccessful { get; }
        public bool IsFailure { get => !IsSuccessful; }
    }
}

namespace CharacterEngineDiscord.Services.AisekaiIntegration.SearchEnums
{
    public enum SearchTime
    {
        all, month, week, yesterday, today
    }

    public enum SearchType
    {
        _new, following, best, top
    }

    public enum SearchSort
    {
        desc, asc
    }
}