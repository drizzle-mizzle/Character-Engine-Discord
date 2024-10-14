using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using Discord;

namespace CharacterEngineDiscord.Helpers.Integrations;


public static class IntegrationsHelper
{
    public static IntegrationType GetIntegrationType(this ISpawnedCharacter spawnedCharacter)
    {
        return spawnedCharacter switch
        {
            SakuraAiSpawnedCharacter => IntegrationType.SakuraAI
        };
    }


    public static string GetStatLabel(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => "Conversations"
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
            IntegrationType.CharacterAI => Color.Blue
        };
    }


    public static string GetCharacterLink(this IntegrationType type, string characterId)
    {
        return type switch
        {
            IntegrationType.SakuraAI => $"https://www.sakura.fm/chat/{characterId}"
        };
    }


    public static string GetAuthorLink(this IntegrationType type, string author)
    {
        return type switch
        {
            IntegrationType.SakuraAI => $"https://www.sakura.fm/user/{author}"
        };
    }

}
