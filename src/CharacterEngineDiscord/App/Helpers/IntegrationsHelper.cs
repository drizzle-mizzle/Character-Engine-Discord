using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Domain.Models.Abstractions.OpenRouter;
using CharacterEngineDiscord.Domain.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Domain.Models.Common;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Domain.Models.Db.Integrations;
using CharacterEngineDiscord.Modules.Abstractions;
using Discord;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.Helpers;


public static class IntegrationsHelper
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


    public static IntegrationType GetIntegrationType(this IGuildIntegration guildIntegration)
    {
        return guildIntegration switch
        {
            ISakuraIntegration => IntegrationType.SakuraAI,
            ICaiIntegration => IntegrationType.CharacterAI,
            IOpenRouterIntegration => IntegrationType.OpenRouter,

            _ => throw new ArgumentOutOfRangeException(nameof(guildIntegration), guildIntegration, null)
        };
    }


    public static CharacterSourceType GetSourceType(this ICharacter character)
    {
        return character switch
        {
            CommonCharacter commonCharacter => commonCharacter.CharacterSourceType,
            ISakuraCharacter => CharacterSourceType.SakuraAI,
            ICaiCharacter => CharacterSourceType.CharacterAI,
            IOpenRouterCharacter orc => orc.CharacterSourceType,

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }


    #region TypeBased

    public static CharacterSourceType GetDefaultSourceType(this IntegrationType integrationType)
    {
        return integrationType switch
        {
            IntegrationType.SakuraAI => CharacterSourceType.SakuraAI,
            IntegrationType.CharacterAI => CharacterSourceType.CharacterAI,

            _ => throw new ArgumentOutOfRangeException(nameof(integrationType), integrationType, null)
        };
    }

    public static IChatModule GetChatModule(this IntegrationType integrationType)
        => integrationType.GetChatModule<IChatModule>();

    public static T GetChatModule<T>(this IntegrationType integrationType) where T : IChatModule
        => integrationType.GetIntegrationModule<T>();

    public static ISearchModule GetSearchModule(this CharacterSourceType sourceType)
        => sourceType.GetSearchModule<ISearchModule>();

    public static T GetSearchModule<T>(this CharacterSourceType sourceType) where T : ISearchModule
        => sourceType.GetIntegrationModule<T>();


    public static T GetIntegrationModule<T>(this IntegrationType integrationType) where T : IModule
    {
        IModule module = integrationType switch
        {
            IntegrationType.SakuraAI => MemoryStorage.IntegrationModules.SakuraAiModule,
            IntegrationType.CharacterAI => MemoryStorage.IntegrationModules.CaiModule,
            IntegrationType.OpenRouter => MemoryStorage.IntegrationModules.OpenRouterModule,

            _ => throw new ArgumentOutOfRangeException(nameof(integrationType), integrationType, null)
        };

        return (T)module;
    }


    public static T GetIntegrationModule<T>(this CharacterSourceType sourceType) where T : IModule
    {
        IModule module = sourceType switch
        {
            CharacterSourceType.SakuraAI => MemoryStorage.IntegrationModules.SakuraAiModule,
            CharacterSourceType.CharacterAI => MemoryStorage.IntegrationModules.CaiModule,
            // CharacterSourceType.ChubAI => MemoryStorage.IntegrationModules.ChubModule,
            // CharacterSourceType.CharacterTavern => MemoryStorage.IntegrationModules.TavernModule,

            _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null)
        };

        return (T)module;
    }


    public static string GetStatLabel(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => "Messages count",
            IntegrationType.CharacterAI => "Chats count",
            IntegrationType.OpenRouter => "Messages count",
        };
    }


    public static string GetIcon(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => BotConfig.SAKURA_AI_EMOJI,
            IntegrationType.CharacterAI => BotConfig.CHARACTER_AI_EMOJI,
            IntegrationType.OpenRouter => BotConfig.OPEN_ROUTER_EMOJI,
        };
    }


    public static Color GetColor(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => Color.Purple,
            IntegrationType.CharacterAI => Color.Blue,
            IntegrationType.OpenRouter => Color.LightGrey,
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

    #endregion


    #region ObjectBased

    public static string GetLinkLabel(this ICharacter character)
    {
        return character.GetIntegrationType() switch
        {
            IntegrationType.SakuraAI or
            IntegrationType.CharacterAI => $"Chat with {character.CharacterName}",
            IntegrationType.OpenRouter => $"{character.CharacterName} on {character.GetSourceType()}"
        };
    }

    public static string GetCharacterLink(this ICharacter character)
    {
        return character.GetSourceType() switch
        {
            CharacterSourceType.SakuraAI => $"https://www.sakura.fm/chat/{character.CharacterId}",
            CharacterSourceType.CharacterAI => $"https://character.ai/chat/{character.CharacterId}",
            CharacterSourceType.ChubAI => $"https://chub.ai/characters/{character.CharacterAuthor}/{character.CharacterId}",
            CharacterSourceType.CharacterTavern => $"https://character-tavern.com/character/{character.CharacterAuthor}/{character.CharacterId}",

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }


    public static string GetAuthorLink(this ICharacter character)
    {
        return character.GetSourceType() switch
        {
            CharacterSourceType.SakuraAI => $"https://www.sakura.fm/user/{character.CharacterAuthor}",
            CharacterSourceType.CharacterAI => $"https://character.ai/profile/{character.CharacterAuthor}",
            CharacterSourceType.ChubAI => $"https://chub.ai/users/{character.CharacterAuthor}",
            CharacterSourceType.CharacterTavern => $"https://character-tavern.com/author/{character.CharacterAuthor}",

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)

        };
    }

    #endregion

    public static async Task EnsureSakuraAiLoginAsync(StoredAction action)
    {
        const IntegrationType type = IntegrationType.SakuraAI;


        var signInAttempt = action.ExtractSakuraAiLoginData();
        var result = await MemoryStorage.IntegrationModules.SakuraAiModule.EnsureLoginByEmailAsync(signInAttempt);

        var sourceInfo = action.ExtractDiscordSourceInfo();
        var channel = (ITextChannel)CharacterEngineBot.DiscordClient.GetChannel(sourceInfo.ChannelId);

        await using var db = DatabaseHelper.GetDbContext();
        var integration = await db.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == channel.GuildId);
        if (integration is not null)
        {
            integration.SakuraEmail = signInAttempt.Email;
            integration.SakuraSessionId = result.SessionId;
            integration.SakuraRefreshToken = result.RefreshToken;
            integration.CreatedAt = DateTime.Now;
        }
        else
        {
            var newSakuraIntegration = new SakuraAiGuildIntegration
            {
                DiscordGuildId = channel.GuildId,
                SakuraEmail = signInAttempt.Email,
                SakuraSessionId = result.SessionId,
                SakuraRefreshToken = result.RefreshToken,
                CreatedAt = DateTime.Now
            };

            MetricsWriter.Create(MetricType.IntegrationCreated, newSakuraIntegration.Id, $"{type:G} | {newSakuraIntegration.SakuraEmail}");
            db.SakuraAiIntegrations.Add(newSakuraIntegration);
        }
        await db.SaveChangesAsync();

        var msg = $"Username: **{result.Username}**\n" +
                  "From now on, this account will be used for all SakuraAI interactions on this server.\n" +
                  type.GetNextStepTail();

        var embed = new EmbedBuilder()
                   .WithTitle($"{type.GetIcon()} SakuraAI user authorized")
                   .WithDescription(msg)
                   .WithColor(type.GetColor())
                   .WithThumbnailUrl(result.UserImageUrl);

        var user = await channel.GetUserAsync(sourceInfo.UserId);
        await channel.SendMessageAsync(user.Mention, embed: embed.Build());
    }

}
