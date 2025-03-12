using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Modules.Adapters;
using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Abstractions.Characters;
using CharacterEngineDiscord.Shared.Abstractions.Sources.CharacterAi;
using CharacterEngineDiscord.Shared.Abstractions.Sources.SakuraAi;
using CharacterEngineDiscord.Shared.Helpers;
using CharacterEngineDiscord.Shared.Models;
using Discord;

namespace CharacterEngine.App.Helpers;


public static class IntegrationsHelper
{
    #region IntegrationType and CharacterSourceType based

    public static string GetIcon(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => BotConfig.SAKURA_AI_EMOJI,
            IntegrationType.CharacterAI => BotConfig.CHARACTER_AI_EMOJI,
            IntegrationType.OpenRouter => BotConfig.OPEN_ROUTER_EMOJI,
        };
    }


    public static string GetIcon(this CharacterSourceType type)
    {
        return type switch
        {
            CharacterSourceType.SakuraAI => BotConfig.SAKURA_AI_EMOJI,
            CharacterSourceType.ChubAI => BotConfig.CHUB_AI_EMOJI
        };
    }


    public static Color GetColor(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => Color.Purple,
            IntegrationType.CharacterAI => Color.Blue,
            IntegrationType.OpenRouter => Color.Green,
        };
    }


    public static string GetServiceLink(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => "https://www.sakura.fm/",
            IntegrationType.CharacterAI => "https://character.ai/",
            IntegrationType.OpenRouter => "https://openrouter.ai",
        };
    }


    public static bool CanNsfw(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => true,
            IntegrationType.CharacterAI => false,
            IntegrationType.OpenRouter => true,
        };
    }

    #endregion


    #region Character

    public static string GetCharacterDescription(this ICharacter character)
    {
        return character switch
        {
            IAdoptedCharacter ac => ac.AdoptedCharacterDescription,

            ISakuraCharacter sc => SakuraCharacterAdapter.GetCharacterDescription(sc),
            ICaiCharacter cc => CaiCharacterAdapter.GetCharacterDescription(cc),

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }


    public static string GetCharacterLink(this ICharacter character)
    {
        CharacterSourceType? characterSourceType = null;

        if (character is CommonCharacter commonCharacter)
        {
            characterSourceType = commonCharacter.CharacterSourceType;
        }
        else if (character is IAdoptedCharacter adoptedCharacter)
        {
            characterSourceType = adoptedCharacter.AdoptedCharacterSourceType;
        }

        if (characterSourceType is not null)
        {
            return characterSourceType switch
            {
                CharacterSourceType.SakuraAI => SakuraCharacterAdapter.GetCharacterLink(character.CharacterId),
                CharacterSourceType.ChubAI => ChubCharacterAdapter.GetCharacterLink(character.CharacterId),

                _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
            };
        }

        return character.GetIntegrationType() switch
        {
            IntegrationType.SakuraAI => SakuraCharacterAdapter.GetCharacterLink(character.CharacterId),
            IntegrationType.CharacterAI => CaiCharacterAdapter.GetCharacterLink(character.CharacterId),

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }

    public static string GetLinkLabel(this ICharacter character)
    {
        return character switch
        {
            IAdoptedCharacter ac => $"{ac.CharacterName} on {ac.AdoptedCharacterSourceType}",

            ISakuraCharacter or ICaiCharacter => $"Chat with {character.CharacterName}",

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }


    // Search only
    public static string GetAuthorLink(this CommonCharacter character)
    {
        if (character.CharacterSourceType is not null)
        {
            return character.CharacterSourceType switch
            {
                CharacterSourceType.SakuraAI => SakuraCharacterAdapter.GetAuthorLink(character.CharacterAuthor),
                CharacterSourceType.ChubAI => ChubCharacterAdapter.GetAuthorLink(character.CharacterId),

                _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
            };
        }

        return character.GetIntegrationType() switch
        {
            IntegrationType.SakuraAI => SakuraCharacterAdapter.GetAuthorLink(character.CharacterAuthor),
            IntegrationType.CharacterAI => CaiCharacterAdapter.GetAuthorLink(character.CharacterAuthor),

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }


    // Search only
    public static string GetStatLabel(this CommonCharacter character)
    {
        if (character.CharacterSourceType is not null)
        {
            return character.CharacterSourceType switch
            {
                CharacterSourceType.SakuraAI => "Messages count",
                CharacterSourceType.ChubAI => "Stars count",

                _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
            };
        }

        return character.GetIntegrationType() switch
        {
            IntegrationType.SakuraAI => "Messages count",
            IntegrationType.CharacterAI => "Chats count",

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }

    #endregion




}
