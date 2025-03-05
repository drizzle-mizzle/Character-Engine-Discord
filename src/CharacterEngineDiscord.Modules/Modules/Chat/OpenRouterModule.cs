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
            var systemMessage = new CharacterChatHistory
            {
                Name = "system",
                Message = orCharacter.AdoptedCharacterDefinition,
                SpawnedCharacterId = spawnedCharacter.Id,
                CreatedAt = DateTime.Now,
            };

            history.Add(systemMessage);
            db.ChatHistories.Add(systemMessage);

            var firstMessage = new CharacterChatHistory()
            {
                Name = "assistant",
                Message = orCharacter.CharacterFirstMessage,
                SpawnedCharacterId = spawnedCharacter.Id,
                CreatedAt = DateTime.Now,
            };

            history.Add(firstMessage);
            db.ChatHistories.Add(firstMessage);
        }

        var newMessage = new CharacterChatHistory
        {
            Name = "user",
            Message = message,
            SpawnedCharacterId = spawnedCharacter.Id,
            CreatedAt = DateTime.Now,
        };

        history.Add(newMessage);
        db.ChatHistories.Add(newMessage);

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

        int attempt = 0;

        while (attempt < 3)
        {
            var response = await _openRouterClient.CompleteAsync(orIntegration.OpenRouterApiKey, model, chatMessages, settings);
            var characterResponse = response.Choices.FirstOrDefault(c => c.Message is not null && !string.IsNullOrWhiteSpace(c.Message.Content))?.Message?.Content;

            if (characterResponse is null)
            {
                attempt++;
                continue;
            }

            characterResponse = characterResponse.Replace($"{spawnedCharacter.CharacterName}:", string.Empty, StringComparison.InvariantCultureIgnoreCase);

            var historyMessage = new CharacterChatHistory()
            {
                Name = "assistant",
                Message = characterResponse,
                SpawnedCharacterId = spawnedCharacter.Id,
                CreatedAt = DateTime.Now
            };

            db.ChatHistories.Add(historyMessage);

            await db.SaveChangesAsync();

            return new CommonCharacterMessage
            {
                Content = characterResponse
            };
        }

        throw new ChatModuleException("No response from OpenRouter");
    }
}
