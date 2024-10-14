using CharacterEngineDiscord.Helpers.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Common;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.Helpers.Mappings;


public static class CommonCharacterMapper
{
    public static CommonCharacter ToCommonCharacter(this ISpawnedCharacter spawnedCharacter)
    {
        var type = spawnedCharacter.GetIntegrationType();
        var link = type.GetCharacterLink(spawnedCharacter.CharacterId);

        var commonCharacter = new CommonCharacter
        {
            IntegrationType = type,
            CharacterId = spawnedCharacter.CharacterId,
            Name = spawnedCharacter.CharacterName,
            ShortDesc = spawnedCharacter.CharacterShortDesc,
            FullDesc = spawnedCharacter.CharacterFullDesc,
            FirstMessage = spawnedCharacter.CharacterFirstMessage,
            Author = spawnedCharacter.CharacterAuthor,
            ImageLink = spawnedCharacter.CharacterImageLink,
            Stat = spawnedCharacter.CharacterStat
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
            ShortDesc = character.description,
            FullDesc = $"**Scenario:**\n{character.scenario}\n**Persona:**\n{character.persona}",
            FirstMessage = character.firstMessage,
            Author = character.creatorUsername,
            ImageLink = character.imageUri,
            Stat = character.messageCount,
            OriginalCharacterModel = character
        };

        return spawnedCharacter;
    }
}
