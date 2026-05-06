using CharacterEngineDiscord.Shared.Abstractions.Sources.OpenRouter;

namespace CharacterEngineDiscord.Shared.Models;


public class OpenRouterSettings : IOpenRouterConfigurable
{

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
}
