using CharacterAi.Client.Models.Common;
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


    public static CommonCharacter ToCommonCharacter(this CaiCharacter character)
    {
        var spawnedCharacter = new CommonCharacter
        {
            CharacterId = character.external_id,
            CharacterName = character.participant__name,
            CharacterFirstMessage = character.greeting,
            CharacterAuthor = character.user__username,
            CharacterImageLink = $"https://characterai.io/i/200/static/avatars/{character.avatar_file_name}?webp=true&anim=0",
            CharacterStat = character.participant__num_interactions.ToString(),
            OriginalCharacterObject = character,
            IntegrationType = IntegrationType.CharacterAI
        };

        return spawnedCharacter;
    }
}
