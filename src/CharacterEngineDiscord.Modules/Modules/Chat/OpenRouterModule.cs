using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Abstractions.OpenRouter;
using CharacterEngineDiscord.Domain.Models.Common;
using CharacterEngineDiscord.Modules.Abstractions;
using OpenRouter.Client;
using UniversalOpenAi.Client.Models;

namespace CharacterEngineDiscord.Modules.Modules.Chat;


public class OpenRouterModule : IChatModule
{
    private readonly OpenRouterClient _openRouterClient = new();


    public async Task<CommonCharacterMessage> CallCharacterAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, ICollection<(string role, string content)> messages)
    {
        var orc = (IOpenRouterCharacter)spawnedCharacter;
        var ori = (IOpenRouterIntegration)guildIntegration;

        var model = orc.OpenRouterModel ?? ori.OpenRouterModel!;
        var chatMessages = messages.Select(m => new ChatMessage
                                    {
                                        Role = m.role,
                                        Content = m.content
                                    })
                                   .ToArray();

        var settings = new GenerationSettings
        {
            Temperature = (float)(orc.OpenRouterTemperature ?? ori.OpenRouterTemperature)!,
            TopP = (float)(orc.OpenRouterTopP ?? ori.OpenRouterTopP)!,
            TopK = (int)(orc.OpenRouterTopK ?? ori.OpenRouterTopK)!,
            FrequencyPenalty = (float)(orc.OpenRouterFrequencyPenalty ?? ori.OpenRouterFrequencyPenalty)!,
            PresencePenalty = (float)(orc.OpenRouterPresencePenalty ?? ori.OpenRouterPresencePenalty)!,
            RepetitionPenalty = (float)(orc.OpenRouterRepetitionPenalty ?? ori.OpenRouterRepetitionPenalty)!,
            MinP = (float)(orc.OpenRouterMinP ?? ori.OpenRouterMinP)!,
            TopA = (float)(orc.OpenRouterTopA ?? ori.OpenRouterTopA)!,
            MaxTokens = (int)(orc.OpenRouterMaxTokens ?? ori.OpenRouterMaxTokens)!
        };

        var response = await _openRouterClient.CompleteAsync(ori.OpenRouterApiKey, model, chatMessages, settings);

        return new CommonCharacterMessage
        {
            Content = response.Choices.First().Message.Content
        };
    }
}
