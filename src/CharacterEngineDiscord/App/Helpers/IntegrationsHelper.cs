using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.IntegrationModules.Abstractions;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Models.Common;
using Discord;

namespace CharacterEngine.App.Helpers;


public static class IntegrationsHelper
{
    public static IntegrationType GetIntegrationType(this ICharacter character)
    {
        if (character is CommonCharacter commonCharacter)
        {
            return commonCharacter.IntegrationType;
        }

        return character switch
        {
            ISakuraCharacter => IntegrationType.SakuraAI,

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }


    #region TypeBased

    public static IIntegrationModule GetIntegrationModule(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => MemoryStorage.IntegrationModules.SakuraAiModule

        };
    }


    public static string GetStatLabel(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => "Messages count"
        };
    }


    public static string GetIcon(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => BotConfig.SAKURA_AI_EMOJI
        };
    }


    public static Color GetColor(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => Color.Purple,
            // IntegrationType.CharacterAI => Color.Blue
        };
    }


    public static string GetLinkPrefix(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => "Chat with"
        };
    }


    #endregion


    #region ObjectBased

    public static string GetCharacterLink(this ICharacter character)
    {
        return character.GetIntegrationType() switch
        {
            IntegrationType.SakuraAI => $"https://www.sakura.fm/chat/{character.CharacterId}",

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }


    public static string GetAuthorLink(this ICharacter character)
    {
        return character.GetIntegrationType() switch
        {
            IntegrationType.SakuraAI => $"https://www.sakura.fm/user/{character.CharacterAuthor}",

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }


    public static string GetStat(this ICharacter character)
    {
        return character switch
        {
            ISakuraCharacter sakuraCharacter => sakuraCharacter.SakuraMessagesCount.ToString(),

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }

    #endregion

}
