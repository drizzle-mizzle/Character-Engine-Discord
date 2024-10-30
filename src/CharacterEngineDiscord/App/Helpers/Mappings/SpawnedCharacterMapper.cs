using CharacterEngineDiscord.Models.Abstractions;
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
        }

        spawnedCharacter.CharacterId = commonCharacter.CharacterId;
        spawnedCharacter.CharacterName = commonCharacter.CharacterName;
        spawnedCharacter.CharacterAuthor = commonCharacter.CharacterAuthor;
        spawnedCharacter.CharacterImageLink = commonCharacter.CharacterImageLink;
        spawnedCharacter.CharacterFirstMessage = commonCharacter.CharacterFirstMessage;

        return spawnedCharacter;
    }
}
