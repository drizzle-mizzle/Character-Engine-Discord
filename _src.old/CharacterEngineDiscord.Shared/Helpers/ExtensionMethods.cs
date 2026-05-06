using CharacterEngineDiscord.Shared.Abstractions;
using CharacterEngineDiscord.Shared.Abstractions.Characters;
using CharacterEngineDiscord.Shared.Abstractions.Sources.CharacterAi;
using CharacterEngineDiscord.Shared.Abstractions.Sources.ChubAi;
using CharacterEngineDiscord.Shared.Abstractions.Sources.OpenRouter;
using CharacterEngineDiscord.Shared.Abstractions.Sources.SakuraAi;
using CharacterEngineDiscord.Shared.Models;

namespace CharacterEngineDiscord.Shared.Helpers;


public static class ExtensionMethods
{
    public static IntegrationType GetIntegrationType(this ICharacter character)
    {
        return character switch
        {
            CommonCharacter commonCharacter => commonCharacter.IntegrationType,

            ISakuraCharacter => IntegrationType.SakuraAI,
            ICaiCharacter => IntegrationType.CharacterAI,
            IOpenRouterCharacter => IntegrationType.OpenRouter,

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }


    public static IntegrationType GetIntegrationType(this IIntegration guildIntegration)
    {
        return guildIntegration switch
        {
            ISakuraIntegration => IntegrationType.SakuraAI,
            ICaiIntegration => IntegrationType.CharacterAI,
            IOpenRouterIntegration => IntegrationType.OpenRouter,

            _ => throw new ArgumentOutOfRangeException(nameof(guildIntegration), guildIntegration, null)
        };
    }


    public static CharacterSourceType GetCharacterSourceType(this IAdoptableCharacter adoptableCharacter)
    {
        return adoptableCharacter switch
        {
            ISakuraCharacter => CharacterSourceType.SakuraAI,
            IChubCharacter => CharacterSourceType.ChubAI,

            _ => throw new ArgumentOutOfRangeException(nameof(adoptableCharacter), adoptableCharacter, null)
        };
    }
}
