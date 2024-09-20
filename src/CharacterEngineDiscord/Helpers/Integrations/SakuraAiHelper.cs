using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using SakuraAi.Models.Common;

namespace CharacterEngine.Helpers.Integrations;


public static class SakuraAiHelper
{
    public static CommonCharacter ToCommonCharacter (this SakuraCharacter character)
    {
        var spawnedCharacter = new CommonCharacter
        {
            CharacterId = character.id,
            CharacterName = character.name,
            CharacterDesc = character.description,
            CharacterFirstMessage = character.firstMessage,
            CharacterImageLink = character.imageUri,
            Stat = character.messageCount,
            Author = character.creatorUsername
        };

        return spawnedCharacter;
    }


    public static ICollection<ICommonCharacter> ToCommonCharacters (this ICollection<SakuraCharacter> characters)
    {
        return characters.Select(character => new CommonCharacter
                          {
                              CharacterId = character.id,
                              CharacterName = character.name,
                              CharacterDesc = character.description,
                              CharacterFirstMessage = character.firstMessage,
                              CharacterImageLink = character.imageUri,
                              Stat = character.messageCount,
                              Author = character.creatorUsername
                          })
                         .Cast<ICommonCharacter>().ToArray();
    }
}
