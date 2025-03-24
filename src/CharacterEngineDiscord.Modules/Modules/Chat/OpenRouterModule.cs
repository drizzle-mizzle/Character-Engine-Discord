using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Modules.Abstractions;
using CharacterEngineDiscord.Modules.Abstractions.Base;
using CharacterEngineDiscord.Modules.Helpers;
using CharacterEngineDiscord.Shared.Abstractions;
using CharacterEngineDiscord.Shared.Abstractions.Characters;
using CharacterEngineDiscord.Shared.Abstractions.Sources.OpenRouter;
using CharacterEngineDiscord.Shared.Models;
using Microsoft.EntityFrameworkCore;
using OpenRouter.Client;
using UniversalOpenAi.Client.Models;

namespace CharacterEngineDiscord.Modules.Modules.Chat;


public class OpenRouterModule : ModuleBase<OpenRouterClient>, IChatModule
{
    private readonly string _connectionString;
    private readonly string _defaultSystemPrompt;


    public OpenRouterModule(string connectionString, string defaultSystemPrompt)
    {
        _connectionString = connectionString;
        _defaultSystemPrompt = defaultSystemPrompt;
    }


    public async Task<CommonCharacterMessage> CallCharacterAsync(ICharacter character, IIntegration integration, string message)
    {
        var orIntegration = (IOpenRouterIntegration)integration;
        var orSpawnedCharacter = (OpenRouterSpawnedCharacter)character;

        await using var db = new AppDbContext(_connectionString);
        var history = await db.ChatHistories
                              .Where(ch => ch.SpawnedCharacterId == orSpawnedCharacter.Id)
                              .ToListAsync();

        if (history.Count == 0)
        {
            var prompt = (orSpawnedCharacter.AdoptedCharacterSystemPrompt ?? orIntegration.SystemPrompt ?? _defaultSystemPrompt)
                   .FillCharacterPlaceholders(orSpawnedCharacter.CharacterName);

            var systemPrompt = $"{prompt}\n{orSpawnedCharacter.AdoptedCharacterDefinition}";

            var systemMessage = new CharacterChatHistory
            {
                Name = "system",
                Message = systemPrompt,
                SpawnedCharacterId = orSpawnedCharacter.Id,
                CreatedAt = DateTime.Now,
            };

            history.Add(systemMessage);
            db.ChatHistories.Add(systemMessage);

            var firstMessage = new CharacterChatHistory()
            {
                Name = "assistant",
                Message = orSpawnedCharacter.CharacterFirstMessage,
                SpawnedCharacterId = orSpawnedCharacter.Id,
                CreatedAt = DateTime.Now,
            };

            history.Add(firstMessage);
            db.ChatHistories.Add(firstMessage);
        }

        var newMessage = new CharacterChatHistory
        {
            Name = "user",
            Message = message,
            SpawnedCharacterId = orSpawnedCharacter.Id,
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
            Temperature = (float)(orSpawnedCharacter.OpenRouterTemperature ?? orIntegration.OpenRouterTemperature)!,
            TopP = (float)(orSpawnedCharacter.OpenRouterTopP ?? orIntegration.OpenRouterTopP)!,
            TopK = (int)(orSpawnedCharacter.OpenRouterTopK ?? orIntegration.OpenRouterTopK)!,
            FrequencyPenalty = (float)(orSpawnedCharacter.OpenRouterFrequencyPenalty ?? orIntegration.OpenRouterFrequencyPenalty)!,
            PresencePenalty = (float)(orSpawnedCharacter.OpenRouterPresencePenalty ?? orIntegration.OpenRouterPresencePenalty)!,
            RepetitionPenalty = (float)(orSpawnedCharacter.OpenRouterRepetitionPenalty ?? orIntegration.OpenRouterRepetitionPenalty)!,
            MinP = (float)(orSpawnedCharacter.OpenRouterMinP ?? orIntegration.OpenRouterMinP)!,
            TopA = (float)(orSpawnedCharacter.OpenRouterTopA ?? orIntegration.OpenRouterTopA)!,
            MaxTokens = (int)(orSpawnedCharacter.OpenRouterMaxTokens ?? orIntegration.OpenRouterMaxTokens)!
        };

        var model = orSpawnedCharacter.OpenRouterModel ?? orIntegration.OpenRouterModel!;

        int attempt = 0;

        while (attempt < 3)
        {
            var response = await _client.CompleteAsync(orIntegration.OpenRouterApiKey, model, chatMessages, settings);
            var characterResponse = response.Choices.FirstOrDefault(c => c.Message is not null && !string.IsNullOrWhiteSpace(c.Message.Content))?.Message?.Content;

            if (characterResponse is null)
            {
                attempt++;
                continue;
            }

            characterResponse = characterResponse.Replace($"{orSpawnedCharacter.CharacterName}:", string.Empty, StringComparison.InvariantCultureIgnoreCase);

            var newChatMessage = new CharacterChatHistory()
            {
                Name = "assistant",
                Message = characterResponse,
                SpawnedCharacterId = orSpawnedCharacter.Id,
                CreatedAt = DateTime.Now
            };

            db.ChatHistories.Add(newChatMessage);

            await db.SaveChangesAsync();

            return new CommonCharacterMessage
            {
                Content = characterResponse
            };
        }

        throw new ChatModuleException("No response from OpenRouter");
    }
}
