using System.Text.Json.Serialization;

namespace CharacterEngineDiscord.Core.Configuration;

/// <summary>
/// File-references for the bot's default messaging templates plus the
/// resolved string contents (filled in by a post-config step that reads the files).
/// Bound from configuration section <c>Messages</c>.
/// </summary>
public sealed class MessagesOptions
{
    public string DefaultMessagesFormatFile { get; init; } = string.Empty;

    public string DefaultSystemPromptFile { get; init; } = string.Empty;

    public string DefaultAvatarFile { get; init; } = string.Empty;

    [JsonIgnore]
    public string? DefaultMessagesFormat { get; set; }

    [JsonIgnore]
    public string? DefaultSystemPrompt { get; set; }
}
