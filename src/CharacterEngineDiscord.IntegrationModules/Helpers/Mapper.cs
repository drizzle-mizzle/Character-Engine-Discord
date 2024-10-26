using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Common;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.IntegrationModules.Helpers;


public static class Mapper
{
    public static CommonCharacter ToCommonCharacter(this SakuraCharacter character)
    {
        var spawnedCharacter = new CommonCharacter
        {
            CharacterId = character.id,
            CharacterName = character.name,
            CharacterFirstMessage = character.firstMessage,
            CharacterAuthor = character.creatorUsername,
            CharacterImageLink = character.imageUri,
            CharacterStat = character.messageCount.ToString(),
            OriginalCharacterObject = character,
            IntegrationType = IntegrationType.SakuraAI
        };

        return spawnedCharacter;
    }
}
