using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.IntegrationModules.Abstractions;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Db;
using CharacterEngineDiscord.Models.Db.Integrations;
using Discord;
using Microsoft.EntityFrameworkCore;
using DI = CharacterEngine.App.Helpers.Infrastructure.DependencyInjectionHelper;

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
            ICaiCharacter => IntegrationType.CharacterAI,

            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };
    }


    public static IntegrationType GetIntegrationType(this IGuildIntegration guildIntegration)
    {
        return guildIntegration switch
        {
            ISakuraIntegration => IntegrationType.SakuraAI,
            ICaiIntegration => IntegrationType.CharacterAI,

            _ => throw new ArgumentOutOfRangeException(nameof(guildIntegration), guildIntegration, null)
        };
    }


    #region TypeBased

    public static IIntegrationModule GetIntegrationModule(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => MemoryStorage.IntegrationModules.SakuraAiModule,
            IntegrationType.CharacterAI => MemoryStorage.IntegrationModules.CaiModule
        };
    }


    public static string GetStatLabel(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => "Messages count",
            IntegrationType.CharacterAI => "Chats count"
        };
    }


    public static string GetIcon(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => BotConfig.SAKURA_AI_EMOJI,
            IntegrationType.CharacterAI => BotConfig.CHARACTER_AI_EMOJI
        };
    }


    public static Color GetColor(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => Color.Purple,
            IntegrationType.CharacterAI => Color.LighterGrey
        };
    }


    public static string GetLinkPrefix(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI or
            IntegrationType.CharacterAI => "Chat with",
        };
    }


    public static string GetServiceLink(this IntegrationType type)
    {
        return type switch
        {
            IntegrationType.SakuraAI => "https://www.sakura.fm/",
            IntegrationType.CharacterAI => "https://character.ai/",
        };
    }

    #endregion


    #region ObjectBased

    public static string GetCharacterLink(this ICharacter character)
    {
        return character.GetIntegrationType() switch
        {
            IntegrationType.SakuraAI => $"https://www.sakura.fm/chat/{character.CharacterId}",
            IntegrationType.CharacterAI => $"https://character.ai/chat/{character.CharacterId}"
        };
    }


    public static string GetAuthorLink(this ICharacter character)
    {
        return character.GetIntegrationType() switch
        {
            IntegrationType.SakuraAI => $"https://www.sakura.fm/user/{character.CharacterAuthor}",
            IntegrationType.CharacterAI => $"https://character.ai/profile/{character.CharacterAuthor}",
        };
    }

    #endregion


    #region Authorizatio

    public static async Task EnsureSakuraAiLoginAsync(StoredAction action)
    {
        await using var db = DatabaseHelper.GetDbContext();

        var signInAttempt = action.ExtractSakuraAiLoginData();
        var result = await MemoryStorage.IntegrationModules.SakuraAiModule.EnsureLoginByEmailAsync(signInAttempt);
        if (result is null)
        {
            action.Attempt++;
            db.StoredActions.Update(action);
            await db.SaveChangesAsync();

            return;
        }

        var sourceInfo = action.ExtractDiscordSourceInfo();
        var channel = (ITextChannel)CharacterEngineBot.DiscordShardedClient.GetChannel(sourceInfo.ChannelId);

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

            MetricsWriter.Create(MetricType.IntegrationCreated, newSakuraIntegration.Id, $"{newSakuraIntegration.GetIntegrationType():G} | {newSakuraIntegration.SakuraEmail}");
            await db.SakuraAiIntegrations.AddAsync(newSakuraIntegration);
        }

        action.Attempt++;
        action.Status = StoredActionStatus.Finished;
        db.StoredActions.Update(action);

        await db.SaveChangesAsync();

        var msg = $"Username: **{result.Username}**\n" +
                  "From now on, this account will be used for all SakuraAI interactions on this server.\n" +
                  "For the next step, use *`/character spawn`* command to spawn new SakuraAI character in this channel.";

        var embed = new EmbedBuilder()
                   .WithTitle($"{IntegrationType.SakuraAI.GetIcon()} SakuraAI user authorized")
                   .WithDescription(msg)
                   .WithColor(IntegrationType.SakuraAI.GetColor())
                   .WithThumbnailUrl(result.UserImageUrl);

        var user = await channel.GetUserAsync(sourceInfo.UserId);
        await channel.SendMessageAsync(user.Mention, embed: embed.Build());
    }


    #endregion
}
