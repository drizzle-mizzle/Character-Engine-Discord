using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Domain.Models.Common;
using CharacterEngineDiscord.Modules.Abstractions;
using CharacterEngineDiscord.Modules.Helpers;
using SakuraAi.Client;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.Modules.Modules.Independent;


public class SakuraAiModule : IChatModule, ISearchModule
{
    private readonly SakuraAiClient _sakuraAiClient = new();

    public async Task<List<CommonCharacter>> SearchAsync(string query, bool allowNsfw, IGuildIntegration? guildIntegration)
    {
        var characters = await _sakuraAiClient.SearchAsync(query, allowNsfw); // TODO: ?
        return characters.Select(sc => sc.ToCommonCharacter()).ToList();
    }


    public async Task<CommonCharacter> GetCharacterInfoAsync(string characterId, IGuildIntegration? _ = null)
    {
        var character = await _sakuraAiClient.GetCharacterInfoAsync(characterId);

        return character.ToCommonCharacter();
    }


    public async Task<CommonCharacterMessage> CallCharacterAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, ICollection<(string role, string content)> messages)
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

            var sakuraChat = await _sakuraAiClient.CreateNewChatAsync(sakuraIntegration.SakuraSessionId, sakuraIntegration.SakuraRefreshToken, character, messages.First().content);
            response = sakuraChat.messages.Last().content;
            sakuraCharacter.SakuraChatId = sakuraChat.chatId;
        }
        else
        {
            var sakuraMessage = await _sakuraAiClient.SendMessageToChatAsync(sakuraIntegration.SakuraSessionId, sakuraIntegration.SakuraRefreshToken, sakuraCharacter.SakuraChatId, messages.First().content);
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
