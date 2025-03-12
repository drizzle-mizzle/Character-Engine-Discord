using CharacterAi.Client;
using CharacterAi.Client.Models;
using CharacterAi.Client.Models.Common;
using CharacterEngineDiscord.Modules.Abstractions;
using CharacterEngineDiscord.Modules.Abstractions.Base;
using CharacterEngineDiscord.Modules.Adapters;
using CharacterEngineDiscord.Shared.Abstractions;
using CharacterEngineDiscord.Shared.Abstractions.Adapters;
using CharacterEngineDiscord.Shared.Abstractions.Characters;
using CharacterEngineDiscord.Shared.Abstractions.Sources.CharacterAi;
using CharacterEngineDiscord.Shared.Models;

namespace CharacterEngineDiscord.Modules.Modules.Universal;


public class CaiModule : ModuleBase<CharacterAiClient>, IChatModule, ISearchModule
{
    public async Task<List<CommonCharacter>> SearchAsync(string query, bool allowNsfw, IIntegration integration)
    {
        var caiIntergration = (ICaiIntegration)integration;
        var characters = await _client.SearchAsync(query, caiIntergration.CaiAuthToken);

        return characters.Select(sc => new CaiCharacterAdapter(sc).ToCommonCharacter()).ToList();
    }


    public async Task<ICharacterAdapter> GetCharacterInfoAsync(string characterId, IIntegration integration)
    {
        var caiIntergration = (ICaiIntegration)integration;
        var character = await _client.GetCharacterInfoAsync(characterId, caiIntergration.CaiAuthToken);

        return new CaiCharacterAdapter(character);
    }


    public Task<CommonCharacterMessage> CallCharacterAsync(ICharacter character, IIntegration integration, string message)
    {
        var caiCharacter = (ICaiCharacter)character;
        var caiIntegration = (ICaiIntegration)integration;

        if (caiCharacter.CaiChatId is null)
        {
            caiCharacter.CaiChatId = _client.CreateNewChat(caiCharacter.CharacterId, caiIntegration.CaiUserId, caiIntegration.CaiAuthToken);
        }

        var data = new CaiSendMessageInputData
        {
            CharacterId = caiCharacter.CharacterId,
            ChatId = caiCharacter.CaiChatId,
            Message = message,
            UserId = caiIntegration.CaiUserId,
            Username = caiIntegration.CaiUsername,
            UserAuthToken = caiIntegration.CaiAuthToken
        };

        var response = _client.SendMessageToChat(data);

        return Task.FromResult(new CommonCharacterMessage
        {
            Content = response
        });
    }


    public Task SendLoginEmailAsync(string email)
        => _client.SendLoginEmailAsync(email);


    public Task<AuthorizedUser> LoginByLinkAsync(string link)
        => _client.LoginByLinkAsync(link);

}
