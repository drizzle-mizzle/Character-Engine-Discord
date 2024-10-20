using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngine.App.Abstractions;


public interface IInterationModule
{
    public Task<List<CommonCharacter>> SearchAsync(string query);

    public Task<string> CreateNewChatAsync(ICharacter character, string firstUserMessage);

    public Task<CommonCharacterMessage> CallAsync(ICharacter character, string message);


}
