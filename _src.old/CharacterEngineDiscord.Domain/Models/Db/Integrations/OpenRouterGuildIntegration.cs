using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using CharacterEngineDiscord.Shared.Abstractions.Sources.OpenRouter;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db.Integrations;


[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
public sealed class OpenRouterGuildIntegration : IOpenRouterIntegration, IGuildIntegration
{
    public Guid Id { get; set; }

    [ForeignKey("DiscordGuild")]
    public ulong DiscordGuildId { get; set; }

    [MaxLength(300)]
    public string? GlobalMessagesFormat { get; set; }

    public required DateTime CreatedAt { get; set; }

    [MaxLength(100)]
    public required string? OpenRouterModel { get; set; }
    public float? OpenRouterTemperature { get; set; } = 1.0f;
    public float? OpenRouterTopP { get; set; } = 1.0f;
    public int? OpenRouterTopK { get; set; } = 40;
    public float? OpenRouterFrequencyPenalty { get; set; } = 1.0f;
    public float? OpenRouterPresencePenalty { get; set; } = 0.0f;
    public float? OpenRouterRepetitionPenalty { get; set; } = 1.0f;
    public float? OpenRouterMinP { get; set; } = 0.0f;
    public float? OpenRouterTopA { get; set; } = 0.0f;
    public int? OpenRouterMaxTokens { get; set; } = 250;

    [MaxLength(100)]
    public required string OpenRouterApiKey { get; set; }


    public DiscordGuild DiscordGuild { get; set; } = null!;

    public bool IsChatOnly
        => true;

    [MaxLength(int.MaxValue)]
    public string? SystemPrompt { get; set; }
}
