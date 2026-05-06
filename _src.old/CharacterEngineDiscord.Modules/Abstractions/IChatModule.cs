using CharacterEngineDiscord.Shared.Abstractions;
using CharacterEngineDiscord.Shared.Abstractions.Characters;
using CharacterEngineDiscord.Shared.Models;

namespace CharacterEngineDiscord.Modules.Abstractions;


public interface IChatModule : IModule
{

    // public Task<(string chatId, string characterMessage)> CreateNewChatAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, string firstUserMessage);

    public Task<CommonCharacterMessage> CallCharacterAsync(ICharacter character, IIntegration integration, string message);


}
