using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Abstractions.OpenRouter;
using CharacterEngineDiscord.Domain.Models.Common;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Modules.Abstractions;
using Microsoft.EntityFrameworkCore;
using OpenRouter.Client;
using UniversalOpenAi.Client.Models;

namespace CharacterEngineDiscord.Modules.Modules.Chat;


public class OpenRouterModule : IChatModule
{
    private readonly string _connectionString;
    private readonly OpenRouterClient _openRouterClient = new();


    public OpenRouterModule(string connectionString)
    {
        _connectionString = connectionString;
    }


    public async Task<CommonCharacterMessage> CallCharacterAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, string message)
    {
        var orIntegration = (IOpenRouterIntegration)guildIntegration;
        var orCharacter = (IOpenRouterCharacter)spawnedCharacter;

        var db = new AppDbContext(_connectionString);
        var history = await db.ChatHistories
                              .Where(ch => ch.SpawnedCharacterId == spawnedCharacter.Id)
                              .ToListAsync();

        if (history.Count == 0)
        {
            history.AddRange(
            [
                new CharacterChatHistory
                {
                    Name = "system",
                    Message = orCharacter.AdoptedCharacterDefinition
                },
                new CharacterChatHistory()
                {
                    Name = "assistant",
                    Message = orCharacter.CharacterFirstMessage
                }
            ]);
        }

        history.Add(new CharacterChatHistory
        {
            Name = "user",
            Message = message
        });

        var chatMessages = history.Select(m => new ChatMessage
                                   {
                                       Role = m.Name,
                                       Content = m.Message
                                   })
                                  .ToArray();

        var settings = new GenerationSettings
        {
            Temperature = (float)(orCharacter.OpenRouterTemperature ?? orIntegration.OpenRouterTemperature)!,
            TopP = (float)(orCharacter.OpenRouterTopP ?? orIntegration.OpenRouterTopP)!,
            TopK = (int)(orCharacter.OpenRouterTopK ?? orIntegration.OpenRouterTopK)!,
            FrequencyPenalty = (float)(orCharacter.OpenRouterFrequencyPenalty ?? orIntegration.OpenRouterFrequencyPenalty)!,
            PresencePenalty = (float)(orCharacter.OpenRouterPresencePenalty ?? orIntegration.OpenRouterPresencePenalty)!,
            RepetitionPenalty = (float)(orCharacter.OpenRouterRepetitionPenalty ?? orIntegration.OpenRouterRepetitionPenalty)!,
            MinP = (float)(orCharacter.OpenRouterMinP ?? orIntegration.OpenRouterMinP)!,
            TopA = (float)(orCharacter.OpenRouterTopA ?? orIntegration.OpenRouterTopA)!,
            MaxTokens = (int)(orCharacter.OpenRouterMaxTokens ?? orIntegration.OpenRouterMaxTokens)!
        };

        var model = orCharacter.OpenRouterModel ?? orIntegration.OpenRouterModel!;
        var response = await _openRouterClient.CompleteAsync(orIntegration.OpenRouterApiKey, model, chatMessages, settings);
        var characterResponse = response.Choices.First().Message.Content;

        history.Add(new CharacterChatHistory() { Name = "assistant", Message = characterResponse });

        await db.SaveChangesAsync();

        return new CommonCharacterMessage
        {
            Content = characterResponse
        };
    }
}
