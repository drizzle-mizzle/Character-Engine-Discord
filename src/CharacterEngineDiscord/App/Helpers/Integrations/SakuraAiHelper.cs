using CharacterEngine.App.Helpers.Mappings;
using CharacterEngineDiscord.Models;

namespace CharacterEngine.App.Helpers.Integrations;


public static class SakuraAiHelper
{
    public static async Task<ICollection<CommonCharacter>> SearchAsync(string query)
    {
        var characters = await RuntimeStorage.SakuraAiClient.SearchAsync(query);
        return characters.Select(sc => sc.ToCommonCharacter()).ToArray();
    }
}
