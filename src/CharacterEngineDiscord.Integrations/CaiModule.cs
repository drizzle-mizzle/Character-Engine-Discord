using CharacterAi.Client;
using CharacterAi.Client.Models;
using CharacterAi.Client.Models.Common;
using CharacterEngineDiscord.IntegrationModules.Abstractions;
using CharacterEngineDiscord.IntegrationModules.Helpers;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;

namespace CharacterEngineDiscord.IntegrationModules;


public class CaiModule : IIntegrationModule
{
    private readonly CharacterAiClient _caiClient = new();


    public async Task<List<CommonCharacter>> SearchAsync(string query, bool allowNsfw, IGuildIntegration guildIntegration)
    {
        var caiIntergration = (ICaiIntegration)guildIntegration;
        var characters = await _caiClient.SearchAsync(query, caiIntergration.CaiAuthToken);
        return characters.Select(sc => sc.ToCommonCharacter()).ToList();
    }


    public async Task<CommonCharacter> GetCharacterAsync(string characterId, IGuildIntegration guildIntegration)
    {
        var caiIntergration = (ICaiIntegration)guildIntegration;
        var character = await _caiClient.GetCharacterInfoAsync(characterId, caiIntergration.CaiAuthToken);
        return character.ToCommonCharacter();
    }


    public async Task<CommonCharacter> GetFullCaiChararcterInfoAsync(string characterId, IGuildIntegration guildIntegration)
    {
        var caiIntergration = (ICaiIntegration)guildIntegration;
        var fullCharacter = await _caiClient.GetCharacterInfoAsync(characterId, caiIntergration.CaiAuthToken);

        return fullCharacter.ToCommonCharacter();
    }


    public async Task<CommonCharacterMessage> CallCharacterAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, string message)
    {
        var caiSpawnedCharacter = (CaiSpawnedCharacter)spawnedCharacter;
        var caiIntegration = (ICaiIntegration)guildIntegration;

        if (caiSpawnedCharacter.CaiChatId is null || caiSpawnedCharacter.ResetWithNextMessage)
        {
            caiSpawnedCharacter.CaiChatId = _caiClient.CreateNewChat(caiSpawnedCharacter.CharacterId, caiIntegration.CaiUserId, caiIntegration.CaiAuthToken);
        }

        var data = new CaiSendMessageInputData
        {
            CharacterId = caiSpawnedCharacter.CharacterId,
            ChatId = caiSpawnedCharacter.CaiChatId,
            Message = message,
            UserId = caiIntegration.CaiUserId,
            Username = caiIntegration.CaiUsername,
            UserAuthToken = caiIntegration.CaiAuthToken
        };

        var response = _caiClient.SendMessageToChat(data);

        return new CommonCharacterMessage
        {
            Content = response
        };
    }


    public Task SendLoginEmailAsync(string email)
        => _caiClient.SendLoginEmailAsync(email);


    public Task<AuthorizedUser> LoginByLinkAsync(string link)
        => _caiClient.LoginByLinkAsync(link);

}
