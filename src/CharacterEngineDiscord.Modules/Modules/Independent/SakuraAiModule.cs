using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Domain.Models.Common;
using CharacterEngineDiscord.Modules.Abstractions;
using CharacterEngineDiscord.Modules.Helpers.Adapters;
using SakuraAi.Client;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.Modules.Modules.Independent;


public class SakuraAiModule : IChatModule, ISearchModule
{
    private readonly SakuraAiClient _sakuraAiClient = new();


    public async Task<List<CommonCharacter>> SearchAsync(string query, bool allowNsfw, IGuildIntegration? guildIntegration)
    {
        var sakuraCharacters = await _sakuraAiClient.SearchAsync(query, allowNsfw); // TODO: ?
        return sakuraCharacters.Select(sc => new SakuraCharacterAdapter(sc).ToCommonCharacter()).ToList();
    }


    public async Task<ICharacterAdapter> GetCharacterInfoAsync(string characterId, IGuildIntegration? _ = null)
    {
        var sakuraCharacter = await _sakuraAiClient.GetCharacterInfoAsync(characterId);
        return new SakuraCharacterAdapter(sakuraCharacter);
    }


    public async Task<CommonCharacterMessage> CallCharacterAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, string message)
    {
        var sakuraCharacter = (ISakuraCharacter)spawnedCharacter;
        var sakuraIntegration = (ISakuraIntegration)guildIntegration;

        string response;
        if (sakuraCharacter.SakuraChatId is null)
        {
            var character = new SakuraCharacter
            {
                id = spawnedCharacter.CharacterId,
                firstMessage = spawnedCharacter.CharacterFirstMessage
            };

            var sakuraChat = await _sakuraAiClient.CreateNewChatAsync(sakuraIntegration.SakuraSessionId, sakuraIntegration.SakuraRefreshToken, character, message);
            response = sakuraChat.messages.Last().content;
            sakuraCharacter.SakuraChatId = sakuraChat.chatId;
        }
        else
        {
            var sakuraMessage = await _sakuraAiClient.SendMessageToChatAsync(sakuraIntegration.SakuraSessionId, sakuraIntegration.SakuraRefreshToken, sakuraCharacter.SakuraChatId, message);
            response = sakuraMessage.content;
        }

        return new CommonCharacterMessage
        {
            Content = response
        };
    }


    public Task<SakuraSignInAttempt> SendLoginEmailAsync(string email)
        => _sakuraAiClient.SendLoginEmailAsync(email);


    public Task<SakuraAuthorizedUser> EnsureLoginByEmailAsync(SakuraSignInAttempt signInAttempt)
        => _sakuraAiClient.EnsureLoginByEmailAsync(signInAttempt);


}
