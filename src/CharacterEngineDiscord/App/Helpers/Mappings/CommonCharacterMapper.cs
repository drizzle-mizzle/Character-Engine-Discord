using CharacterEngine.App.Helpers.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using SakuraAi.Client.Models.Common;

namespace CharacterEngine.App.Helpers.Mappings;


public static class CommonCharacterMapper
{
    public static CommonCharacter ToCommonCharacter(this ISpawnedCharacter spawnedCharacter)
    {
        var type = spawnedCharacter switch
        {
            SakuraAiSpawnedCharacter => IntegrationType.SakuraAI
        };

        var link = type.GetCharacterLink(spawnedCharacter.CharacterId);

        var commonCharacter = new CommonCharacter
        {
            IntegrationType = type,
            CharacterId = spawnedCharacter.CharacterId,
            Name = spawnedCharacter.CharacterName,
            Desc = spawnedCharacter.CharacterDesc,
            FirstMessage = spawnedCharacter.CharacterFirstMessage,
            Author = spawnedCharacter.CharacterAuthor,
            ImageLink = spawnedCharacter.CharacterImageLink,
            Stat = spawnedCharacter.CharacterStat,
            OriginalLink = link
        };

        return commonCharacter;
    }


    public static CommonCharacter ToCommonCharacter(this SakuraCharacter character)
    {
        var spawnedCharacter = new CommonCharacter
        {
            IntegrationType = IntegrationType.SakuraAI,
            CharacterId = character.id,
            Name = character.name,
            Desc = character.description,
            FirstMessage = character.firstMessage,
            Author = character.creatorUsername,
            ImageLink = character.imageUri,
            Stat = character.messageCount,
            OriginalLink = IntegrationType.SakuraAI.GetCharacterLink(character.id),
            OriginalCharacterModel = character
        };

        return spawnedCharacter;
    }
}
