using CharacterAi.Client;
using CharacterAi.Client.Models;
using CharacterAi.Client.Models.Common;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Domain.Models.Common;
using CharacterEngineDiscord.Modules.Abstractions;
using CharacterEngineDiscord.Modules.Helpers;

namespace CharacterEngineDiscord.Modules.Modules.Independent;


public class CaiModule : IChatModule, ISearchModule
{
    private readonly CharacterAiClient _caiClient = new();


    public async Task<List<CommonCharacter>> SearchAsync(string query, bool allowNsfw, IGuildIntegration? guildIntegration)
    {
        var caiIntergration = (ICaiIntegration)guildIntegration!;
        var characters = await _caiClient.SearchAsync(query, caiIntergration.CaiAuthToken);
        return characters.Select(sc => sc.ToCommonCharacter()).ToList();
    }


    public async Task<CommonCharacter> GetCharacterInfoAsync(string characterId, IGuildIntegration? guildIntegration)
    {
        var caiIntergration = (ICaiIntegration)guildIntegration!;
        var character = await _caiClient.GetCharacterInfoAsync(characterId, caiIntergration.CaiAuthToken);
        return character.ToCommonCharacter();
    }


    public Task<CommonCharacterMessage> CallCharacterAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, ICollection<(string role, string content)> messages)
    {
        var caiCharacter = (ICaiCharacter)spawnedCharacter;
        var caiIntegration = (ICaiIntegration)guildIntegration;

        if (caiCharacter.CaiChatId is null)
        {
            caiCharacter.CaiChatId = _caiClient.CreateNewChat(caiCharacter.CharacterId, caiIntegration.CaiUserId, caiIntegration.CaiAuthToken);
        }

        var data = new CaiSendMessageInputData
        {
            CharacterId = caiCharacter.CharacterId,
            ChatId = caiCharacter.CaiChatId,
            Message = messages.First().content,
            UserId = caiIntegration.CaiUserId,
            Username = caiIntegration.CaiUsername,
            UserAuthToken = caiIntegration.CaiAuthToken
        };

        var response = _caiClient.SendMessageToChat(data);

        return Task.FromResult(new CommonCharacterMessage
        {
            Content = response
        });
    }


    public Task SendLoginEmailAsync(string email)
        => _caiClient.SendLoginEmailAsync(email);


    public Task<AuthorizedUser> LoginByLinkAsync(string link)
        => _caiClient.LoginByLinkAsync(link);

}
