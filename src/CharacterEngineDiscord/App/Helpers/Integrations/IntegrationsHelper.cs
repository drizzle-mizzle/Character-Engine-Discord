using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using Discord;

namespace CharacterEngine.App.Helpers.Integrations;


public static class IntegrationsHelper
{
    public static IntegrationType GetIntegrationType(this ISpawnedCharacter spawnedCharacter)
    {
        return spawnedCharacter switch
        {
            SakuraAiSpawnedCharacter => IntegrationType.SakuraAI
        };
    }

    public static string GetIcon(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => MessagesTemplates.SAKURA_EMOJI
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
            IntegrationType.SakuraAI => $"https://www.sakura.fm/ru/chat/{characterId}"
        };
    }
}
