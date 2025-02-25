using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Common;

namespace CharacterEngineDiscord.Modules.Abstractions;


public interface ISearchModule : IModule
{
    public Task<List<CommonCharacter>> SearchAsync(string query, bool allowNsfw, IGuildIntegration? guildIntegration = null);

    public Task<ICharacterAdapter> GetCharacterInfoAsync(string characterId, IGuildIntegration? guildIntegration = null);
}
