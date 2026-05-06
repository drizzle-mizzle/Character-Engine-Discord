using CharacterEngineDiscord.Modules.Abstractions;
using CharacterEngineDiscord.Modules.Abstractions.Base;
using CharacterEngineDiscord.Modules.Adapters;
using CharacterEngineDiscord.Modules.Clients.ChubAiClient;
using CharacterEngineDiscord.Modules.Clients.ChubAiClient.Models;
using CharacterEngineDiscord.Shared.Abstractions;
using CharacterEngineDiscord.Shared.Abstractions.Adapters;
using CharacterEngineDiscord.Shared.Helpers;
using CharacterEngineDiscord.Shared.Models;

namespace CharacterEngineDiscord.Modules.Modules.Search;


public class ChubAiModule : ModuleBase<ChubAiClient>, ISearchModule
{
    public async Task<List<CommonCharacter>> SearchAsync(string query, bool allowNsfw, IIntegration integration)
    {
        var characters = await _client.SearchAsync(query, allowNsfw ? ChubAiClient.NsfwMode.allowNSFW : ChubAiClient.NsfwMode.noNSFW);
        var integrationType = integration.GetIntegrationType();

        return characters.Select(NewCommonCharacter).ToList();

        CommonCharacter NewCommonCharacter(ChubAiCharacter sc)
            => new ChubCharacterAdapter(sc, integrationType).ToCommonCharacter();
    }


    public async Task<ICharacterAdapter> GetCharacterInfoAsync(string fullPath, IIntegration integration)
    {
        var character = await _client.GetCharacterInfoAsync(fullPath);

        return new ChubCharacterAdapter(character, integration.GetIntegrationType());
    }
}
