using CharacterEngineDiscord.Shared.Abstractions;
using CharacterEngineDiscord.Shared.Abstractions.Adapters;
using CharacterEngineDiscord.Shared.Models;

namespace CharacterEngineDiscord.Modules.Abstractions;


public interface ISearchModule : IModule
{
    public Task<List<CommonCharacter>> SearchAsync(string query, bool allowNsfw, IIntegration integration);

    public Task<ICharacterAdapter> GetCharacterInfoAsync(string characterId, IIntegration integration);
}
