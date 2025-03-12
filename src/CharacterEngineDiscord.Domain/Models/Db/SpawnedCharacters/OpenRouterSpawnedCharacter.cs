using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Abstractions.Adapters;
using CharacterEngineDiscord.Shared.Abstractions.Sources.OpenRouter;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;


[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
public class OpenRouterSpawnedCharacter : IOpenRouterCharacter, ISpawnedCharacter
{
    public OpenRouterSpawnedCharacter()
    {

    }


    public OpenRouterSpawnedCharacter(IAdoptableCharacterAdapter characterAdapter, IOpenRouterIntegration openRouterIntegration)
    {
        CharacterName = characterAdapter.GetCharacterName();
        AdoptedCharacterSourceType = characterAdapter.GetCharacterSourceType();
        AdoptedCharacterDescription = characterAdapter.GetCharacterDescription();
        AdoptedCharacterDefinition = characterAdapter.GetCharacterDefinition();
        AdoptedCharacterLink = characterAdapter.GetCharacterLink();
        AdoptedCharacterAuthorLink = characterAdapter.GetAuthorLink();

        OpenRouterModel = openRouterIntegration.OpenRouterModel;
        OpenRouterTemperature = openRouterIntegration.OpenRouterTemperature;
        OpenRouterTopP = openRouterIntegration.OpenRouterTopP;
        OpenRouterTopK = openRouterIntegration.OpenRouterTopK;
        OpenRouterFrequencyPenalty = openRouterIntegration.OpenRouterFrequencyPenalty;
        OpenRouterPresencePenalty = openRouterIntegration.OpenRouterPresencePenalty;
        OpenRouterRepetitionPenalty = openRouterIntegration.OpenRouterRepetitionPenalty;
        OpenRouterMinP = openRouterIntegration.OpenRouterMinP;
        OpenRouterTopA = openRouterIntegration.OpenRouterTopA;
        OpenRouterMaxTokens = openRouterIntegration.OpenRouterMaxTokens;
    }


    public Guid Id { get; init; } = Guid.NewGuid();

    [ForeignKey("DiscordChannel")]
    public ulong DiscordChannelId { get; set; }

    public ulong WebhookId { get; set; }

    [MaxLength(100)]
    public string WebhookToken { get; set; } = null!;

    [MaxLength(50)]
    public string CallPrefix { get; set; } = null!;

    [MaxLength(300)]
    public string? MessagesFormat { get; set; }

    public uint ResponseDelay { get; set; }
    public double FreewillFactor { get; set; }

    public uint FreewillContextSize { get; set; }
    public bool EnableSwipes { get; set; }
    public bool EnableWideContext { get; set; }
    public bool EnableQuotes { get; set; }
    public bool EnableStopButton { get; set; }
    public bool SkipNextBotMessage { get; set; }
    public ulong LastCallerDiscordUserId { get; set; }
    public ulong LastDiscordMessageId { get; set; }
    public uint MessagesSent { get; set; }
    public DateTime LastCallTime { get; set; }

    [MaxLength(100)]
    public string CharacterId { get; set; } = null!;

    [MaxLength(50)]
    public string CharacterName { get; set; } = null!;

    [MaxLength(int.MaxValue)]
    public string CharacterFirstMessage { get; set; } = null!;

    [MaxLength(500)]
    public string? CharacterImageLink { get; set; }

    [MaxLength(50)]
    public string CharacterAuthor { get; set; } = null!;

    public bool IsNfsw { get; set; }


    [MaxLength(100)]
    public string? OpenRouterModel { get; set; }
    public float? OpenRouterTemperature { get; set; }
    public float? OpenRouterTopP { get; set; }
    public int? OpenRouterTopK { get; set; }
    public float? OpenRouterFrequencyPenalty { get; set; }
    public float? OpenRouterPresencePenalty { get; set; }
    public float? OpenRouterRepetitionPenalty { get; set; }
    public float? OpenRouterMinP { get; set; }
    public float? OpenRouterTopA { get; set; }
    public int? OpenRouterMaxTokens { get; set; }

    public DiscordChannel? DiscordChannel { get; set; }

    public CharacterSourceType AdoptedCharacterSourceType { get; set; }

    [MaxLength(int.MaxValue)]
    public string? AdoptedCharacterSystemPrompt { get; set; }

    [MaxLength(int.MaxValue)]
    public string AdoptedCharacterDefinition { get; set; }

    [MaxLength(int.MaxValue)]
    public string AdoptedCharacterDescription { get; set; }

    [MaxLength(500)]
    public string AdoptedCharacterLink { get; set; }

    [MaxLength(200)]
    public string AdoptedCharacterAuthorLink { get; set; }
}
