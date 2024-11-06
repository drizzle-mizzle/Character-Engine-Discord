using CharacterAi.Client;
using CharacterAi.Client.Models.Common;
using CharacterEngineDiscord.IntegrationModules.Abstractions;
using CharacterEngineDiscord.IntegrationModules.Helpers;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngineDiscord.IntegrationModules;


public class CaiModule : IIntegrationModule
{
    private readonly CharacterAiClient _caiClient = new();


    public async Task<List<CommonCharacter>> SearchAsync(string query, IGuildIntegration guildIntegration)
    {
        var caiIntergration = (ICaiIntegration)guildIntegration;
        var characters = await _caiClient.SearchAsync(query, caiIntergration.CaiAuthToken);
        return characters.Select(sc => sc.ToCommonCharacter()).ToList();
    }


    public async Task<CommonCharacter> GetFullCaiChararcterInfoAsync(string characterId, IGuildIntegration guildIntegration)
    {
        var caiIntergration = (ICaiIntegration)guildIntegration;
        var fullCharacter = await _caiClient.GetCharacterInfoAsync(characterId, caiIntergration.CaiAuthToken);

        return fullCharacter.ToCommonCharacter();
    }


    public Task<(string chatId, string? characterMessage)> CreateNewChatAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, string firstUserMessage)
    {
        var caiIntegration = (ICaiIntegration)guildIntegration;


        return null;
    }


    public Task<CommonCharacterMessage> CallCharacterAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, string message)
    {

        return null;
    }


    public Task SendLoginEmailAsync(string email)
        => _caiClient.SendLoginEmailAsync(email);


    public Task<AuthorizedUser> LoginByLinkAsync(string link)
        => _caiClient.LoginByLinkAsync(link);

}
