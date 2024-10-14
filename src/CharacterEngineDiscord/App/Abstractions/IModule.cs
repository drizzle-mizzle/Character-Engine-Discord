using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngine.App.Abstractions;


public interface IModule
{
    public Task<List<CommonCharacter>> SearchAsync(string query);

    public Task<string> CreateNewChatAsync(ISpawnedCharacter spawnedCharacter, string firstUserMessage);

    public Task<CommonCharacterMessage> CallAsync(ISpawnedCharacter spawnedCharacter, string message);


}
