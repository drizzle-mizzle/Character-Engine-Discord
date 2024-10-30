using CharacterEngineDiscord.IntegrationModules.Abstractions;
using CharacterEngineDiscord.IntegrationModules.Helpers;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using SakuraAi.Client;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.IntegrationModules;


public class SakuraAiModule : IIntegrationModule
{
    private readonly SakuraAiClient _sakuraAiClient = new();

    public async Task<List<CommonCharacter>> SearchAsync(string query)
    {
        var characters = await _sakuraAiClient.SearchAsync(query);
        return characters.Select(sc => sc.ToCommonCharacter()).ToList();
    }


    public async Task<(string chatId, string? characterMessage)> CreateNewChatAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, string firstUserMessage)
    {
        var sakuraIntegration = (ISakuraIntegration)guildIntegration;

        var sakuraCharacter = new SakuraCharacter
        {
            id = spawnedCharacter.CharacterId,
            firstMessage = spawnedCharacter.CharacterFirstMessage
        };

        var sakuraChat = await _sakuraAiClient.CreateNewChatAsync(sakuraIntegration.SakuraSessionId, sakuraIntegration.SakuraRefreshToken, sakuraCharacter, firstUserMessage);

        return (sakuraChat.chatId, sakuraChat.messages.Last().content);
    }


    public async Task<CommonCharacterMessage> CallCharacterAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, string message)
    {
        var spawnedSakuraCharacter = (SakuraAiSpawnedCharacter)spawnedCharacter;
        var sakuraIntegration = (ISakuraIntegration)guildIntegration;

        string response;
        if (spawnedSakuraCharacter.SakuraChatId is null || spawnedSakuraCharacter.ResetWithNextMessage)
        {
            var newSakuraChat = await CreateNewChatAsync(spawnedCharacter, guildIntegration, message);
            spawnedSakuraCharacter.SakuraChatId = newSakuraChat.chatId;
            response = newSakuraChat.characterMessage!;
        }
        else
        {
            var sakuraMessage =await _sakuraAiClient.SendMessageToChatAsync(sakuraIntegration.SakuraSessionId, sakuraIntegration.SakuraRefreshToken, spawnedSakuraCharacter.SakuraChatId, message);
            response = sakuraMessage.content;
        }

        return new CommonCharacterMessage
        {
            Content = response
        };
    }


    public Task<SakuraSignInAttempt> SendLoginEmailAsync(string email)
        => _sakuraAiClient.SendLoginEmailAsync(email);


    public Task<SakuraAuthorizedUser?> EnsureLoginByEmailAsync(SakuraSignInAttempt signInAttempt)
        => _sakuraAiClient.EnsureLoginByEmailAsync(signInAttempt);


}
