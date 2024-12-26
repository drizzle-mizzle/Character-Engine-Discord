using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Common;

namespace CharacterEngineDiscord.Modules.Abstractions;


public interface IChatModule : IModule
{

    // public Task<(string chatId, string characterMessage)> CreateNewChatAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, string firstUserMessage);

    public Task<CommonCharacterMessage> CallCharacterAsync(ISpawnedCharacter spawnedCharacter, IGuildIntegration guildIntegration, ICollection<(string role, string content)> messages);


}
