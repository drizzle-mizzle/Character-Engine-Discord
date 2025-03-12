using CharacterEngineDiscord.Modules.Abstractions;
using CharacterEngineDiscord.Modules.Abstractions.Base;
using CharacterEngineDiscord.Modules.Adapters;
using CharacterEngineDiscord.Shared.Abstractions;
using CharacterEngineDiscord.Shared.Abstractions.Adapters;
using CharacterEngineDiscord.Shared.Abstractions.Characters;
using CharacterEngineDiscord.Shared.Abstractions.Sources.SakuraAi;
using CharacterEngineDiscord.Shared.Models;
using SakuraAi.Client;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.Modules.Modules.Universal;


public class SakuraAiModule : ModuleBase<SakuraAiClient>, IChatModule, ISearchModule
{
    public async Task<List<CommonCharacter>> SearchAsync(string query, bool allowNsfw, IIntegration _ = null!)
    {
        var sakuraCharacters = await _client.SearchAsync(query, allowNsfw);
        return sakuraCharacters.Select(sc => new SakuraCharacterAdapter(sc).ToCommonCharacter()).ToList();
    }


    public async Task<ICharacterAdapter> GetCharacterInfoAsync(string characterId, IIntegration _ = null!)
    {
        var sakuraCharacter = await _client.GetCharacterInfoAsync(characterId);
        return new SakuraCharacterAdapter(sakuraCharacter);
    }


    public async Task<CommonCharacterMessage> CallCharacterAsync(ICharacter character, IIntegration integration, string message)
    {
        var sakuraCharacter = (ISakuraCharacter)character;
        var sakuraIntegration = (ISakuraIntegration)integration;

        string response;
        if (sakuraCharacter.SakuraChatId is null)
        {
            var sakuraClientCharacter = new SakuraCharacter
            {
                id = character.CharacterId,
                firstMessage = character.CharacterFirstMessage
            };

            var sakuraChat = await _client.CreateNewChatAsync(sakuraIntegration.SakuraSessionId, sakuraIntegration.SakuraRefreshToken, sakuraClientCharacter, message);

            response = sakuraChat.messages.Last().content;
            sakuraCharacter.SakuraChatId = sakuraChat.chatId;
        }
        else
        {
            var sakuraMessage = await _client.SendMessageToChatAsync(sakuraIntegration.SakuraSessionId, sakuraIntegration.SakuraRefreshToken, sakuraCharacter.SakuraChatId, message);
            response = sakuraMessage.content;
        }

        return new CommonCharacterMessage
        {
            Content = response
        };
    }


    public Task<SakuraSignInAttempt> SendLoginEmailAsync(string email)
        => _client.SendLoginEmailAsync(email);


    public Task<SakuraAuthorizedUser> EnsureLoginByEmailAsync(SakuraSignInAttempt signInAttempt)
        => _client.EnsureLoginByEmailAsync(signInAttempt);


}
