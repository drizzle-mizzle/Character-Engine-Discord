using CharacterAi.Client.Models.Common;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Models.Common;
using SakuraAi.Client.Models.Common;

namespace CharacterEngine.App.Helpers.Mappings;


public static class SpawnedCharacterMapper
{
    public static ISpawnedCharacter FillWith(this ISpawnedCharacter spawnedCharacter, CommonCharacter commonCharacter)
    {
        switch (spawnedCharacter)
        {
            case ISakuraCharacter sakuraCharacterCasted:
            {
                var sakuraCharacter = (SakuraCharacter)commonCharacter.OriginalCharacterObject!;
                sakuraCharacterCasted.SakuraMessagesCount = sakuraCharacter.messageCount;
                sakuraCharacterCasted.SakuraDescription = sakuraCharacter.description;
                sakuraCharacterCasted.SakuraPersona = sakuraCharacter.persona;
                sakuraCharacterCasted.SakuraScenario = sakuraCharacter.scenario;

                break;
            }
            case ICaiCharacter caiCharacterCasted:
            {
                var caiCharacter = (CaiCharacter)commonCharacter.OriginalCharacterObject!;
                caiCharacterCasted.CaiTitle = caiCharacter.title!;
                caiCharacterCasted.CaiDescription = caiCharacter.description!;
                caiCharacterCasted.CaiDefinition = caiCharacter.definition;
                caiCharacterCasted.CaiImageGenEnabled = caiCharacter.img_gen_enabled;

                break;
            }
        }

        return spawnedCharacter;
    }
}
