using CharacterEngine.App.Abstractions;
using CharacterEngineDiscord.Helpers.Common;
using CharacterEngineDiscord.Helpers.Mappings;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Db.Integrations;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using SakuraAi.Client;
using SakuraAi.Client.Models.Common;

namespace CharacterEngine.App.Modules;


public class SakuraAiModule : IModule
{
    private static readonly SakuraAiClient _sakuraAiClient = new();


    public async Task<List<CommonCharacter>> SearchAsync(string query)
    {
        var characters = await _sakuraAiClient.SearchAsync(query);
        return characters.Select(sc => sc.ToCommonCharacter()).ToList();
    }


    public async Task<string> CreateNewChatAsync(ISpawnedCharacter spawnedCharacter, string firstUserMessage)
    {
        var sakuraIntegration = (SakuraAiIntegration)await spawnedCharacter.GetIntegrationAsync();

        var sakuraCharacter = new SakuraCharacter
        {
            id = spawnedCharacter.CharacterId,
            firstMessage = spawnedCharacter.CharacterFirstMessage
        };

        var response = await _sakuraAiClient.CreateNewChatAsync(sakuraIntegration.SessionId, sakuraIntegration.RefreshToken, sakuraCharacter, firstUserMessage);

        return response;
    }


    public async Task<CommonCharacterMessage> CallAsync(ISpawnedCharacter spawnedCharacter, string message)
    {
        var sakuraCharacter = (SakuraAiSpawnedCharacter)spawnedCharacter;
        var sakuraIntegration = (SakuraAiIntegration)await spawnedCharacter.GetIntegrationAsync();

        var response = await _sakuraAiClient.SendMessageToChatAsync(sakuraIntegration.SessionId, sakuraIntegration.RefreshToken, sakuraCharacter.SakuraChatId, message);

        return new CommonCharacterMessage
        {
            Content = response.content
        };
    }


    public static Task<SakuraSignInAttempt> SendLoginEmailAsync(string email)
        => _sakuraAiClient.SendLoginEmailAsync(email);


    public static Task<SakuraAuthorizedUser?> EnsureLoginByEmailAsync(SakuraSignInAttempt signInAttempt)
        => _sakuraAiClient.EnsureLoginByEmailAsync(signInAttempt);


}
