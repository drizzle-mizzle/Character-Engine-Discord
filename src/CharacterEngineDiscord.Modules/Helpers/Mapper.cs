using CharacterAi.Client.Models.Common;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Domain.Models.Abstractions.OpenRouter;
using CharacterEngineDiscord.Domain.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Domain.Models.Common;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.Modules.Helpers;


public static class Mapper
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
            case IOpenRouterCharacter openRouterCharacter:
            {
                openRouterCharacter.FillWith(commonCharacter.OriginalCharacterObject);

                break;
            }
        }

        return spawnedCharacter;
    }


    public static void FillWith(this ICharacterCard characterCard, object? originalCharacterObject)
    {
        switch (originalCharacterObject)
        {
            case SakuraCharacter sakuraCharacter:
            {
                characterCard.CardCharacterDescription = sakuraCharacter.description;
                break;
            }
        }
    }


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
            IntegrationType = IntegrationType.SakuraAI,
            CharacterSourceType = CharacterSourceType.SakuraAI,
            IsNfsw = character.nsfw
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
            IntegrationType = IntegrationType.CharacterAI,
            CharacterSourceType = CharacterSourceType.CharacterAI,
            IsNfsw = false
        };

        return spawnedCharacter;
    }

}
