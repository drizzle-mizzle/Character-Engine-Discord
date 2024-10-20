using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using Newtonsoft.Json.Linq;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.Helpers.Mappings;


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

        var characterCasted = (ICharacter)spawnedCharacter;
        characterCasted.CharacterId = commonCharacter.CharacterId;
        characterCasted.CharacterName = commonCharacter.CharacterName;
        characterCasted.CharacterAuthor = commonCharacter.CharacterAuthor;
        characterCasted.CharacterImageLink = commonCharacter.CharacterImageLink;
        characterCasted.CharacterFirstMessage = commonCharacter.CharacterFirstMessage;

        return spawnedCharacter;
    }
}
