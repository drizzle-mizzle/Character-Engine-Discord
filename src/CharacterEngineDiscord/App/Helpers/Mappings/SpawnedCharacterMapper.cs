using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngineDiscord.Helpers.Mappings;


public static class SpawnedCharacterMapper
{
    public static ISpawnedCharacter FillWith(this ISpawnedCharacter spawnedCharacter, CommonCharacter commonCharacter)
    {
        spawnedCharacter.CharacterId = commonCharacter.CharacterId;
        spawnedCharacter.CharacterName = commonCharacter.Name;
        spawnedCharacter.CharacterShortDesc = commonCharacter.ShortDesc;
        spawnedCharacter.CharacterFullDesc = commonCharacter.FullDesc;
        spawnedCharacter.CharacterFirstMessage = commonCharacter.FirstMessage;
        spawnedCharacter.CharacterImageLink = commonCharacter.ImageLink;
        spawnedCharacter.CharacterAuthor = commonCharacter.Author;
        spawnedCharacter.CharacterStat = commonCharacter.Stat;

        return spawnedCharacter;
    }
}
