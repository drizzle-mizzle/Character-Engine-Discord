using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;

namespace CharacterEngine.App.Helpers.Mappings;


public static class SpawnedCharacterMapper
{
    public static ISpawnedCharacter FillWith(this ISpawnedCharacter spawnedCharacter, CommonCharacter commonCharacter)
    {
        spawnedCharacter.CharacterId = commonCharacter.CharacterId;
        spawnedCharacter.CharacterName = commonCharacter.Name;
        spawnedCharacter.CharacterDesc = commonCharacter.Desc;
        spawnedCharacter.CharacterFirstMessage = commonCharacter.FirstMessage;
        spawnedCharacter.CharacterImageLink = commonCharacter.ImageLink;
        spawnedCharacter.CharacterAuthor = commonCharacter.Author;
        spawnedCharacter.CharacterStat = commonCharacter.Stat;

        return spawnedCharacter;
    }
}
