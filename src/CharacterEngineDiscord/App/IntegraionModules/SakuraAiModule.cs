// ReSharper disable SuspiciousTypeConversion.Global
using CharacterEngine.App.Abstractions;
using CharacterEngine.App.Helpers;
using CharacterEngineDiscord.Helpers.Common;
using CharacterEngineDiscord.Helpers.Mappings;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using SakuraAi.Client;
using SakuraAi.Client.Models.Common;

namespace CharacterEngine.App.IntegraionModules;


public class SakuraAiModule : IInterationModule
{
    private readonly SakuraAiClient _sakuraAiClient = new();

    public async Task<List<CommonCharacter>> SearchAsync(string query)
    {
        var characters = await _sakuraAiClient.SearchAsync(query);
        return characters.Select(sc => sc.ToCommonCharacter()).ToList();
    }


    public async Task<string> CreateNewChatAsync(ICharacter character, string firstUserMessage)
    {
        var spawnedSakuraCharacter = (SakuraAiSpawnedCharacter)character;
        var sakuraIntegration = (ISakuraIntegration)DatabaseHelper.GetGuildIntegrationAsync(spawnedSakuraCharacter);

        var sakuraCharacter = new SakuraCharacter
        {
            id = character.CharacterId,
            firstMessage = character.CharacterFirstMessage
        };

        var response = await _sakuraAiClient.CreateNewChatAsync(sakuraIntegration.SakuraSessionId, sakuraIntegration.SakuraRefreshToken, sakuraCharacter, firstUserMessage);

        return response;
    }


    public async Task<CommonCharacterMessage> CallAsync(ICharacter character, string message)
    {
        var spawnedSakuraCharacter = (SakuraAiSpawnedCharacter)character;
        var sakuraIntegration = (ISakuraIntegration)DatabaseHelper.GetGuildIntegrationAsync(spawnedSakuraCharacter);

        if (spawnedSakuraCharacter.SakuraChatId is null || spawnedSakuraCharacter.ResetWithNextMessage)
        {
            spawnedSakuraCharacter.SakuraChatId = await CreateNewChatAsync(character, message);
        }

        var response = await _sakuraAiClient.SendMessageToChatAsync(sakuraIntegration.SakuraSessionId, sakuraIntegration.SakuraRefreshToken, spawnedSakuraCharacter.SakuraChatId, message);

        return new CommonCharacterMessage
        {
            Content = response.content
        };
    }


    public Task<SakuraSignInAttempt> SendLoginEmailAsync(string email)
        => _sakuraAiClient.SendLoginEmailAsync(email);


    public Task<SakuraAuthorizedUser?> EnsureLoginByEmailAsync(SakuraSignInAttempt signInAttempt)
        => _sakuraAiClient.EnsureLoginByEmailAsync(signInAttempt);


}
