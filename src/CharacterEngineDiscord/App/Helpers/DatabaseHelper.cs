using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Helpers.Integrations;
using CharacterEngineDiscord.Helpers.Mappings;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Db.Discord;
using CharacterEngineDiscord.Models.Db.Integrations;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.Helpers;


/// <summary>
/// Kind of a repository, but only for tricky db operations
/// </summary>
public static class DatabaseHelper
{
    public static AppDbContext GetDbContext() => new(BotConfig.DATABASE_CONNECTION_STRING);


    #region SpawnedCharacters

    public static async Task<List<ISpawnedCharacter>> GetAllSpawnedCharactersAsync()
    {
        await using var db = GetDbContext();
        var characters = await db.SakuraAiSpawnedCharacters.ToListAsync()
                      ?? [];

        return characters.ToList<ISpawnedCharacter>();
    }


    public static async Task<ISpawnedCharacter?> GetSpawnedCharacterByIdAsync(Guid characterId)
    {
        await using var db = GetDbContext();

        return await db.SakuraAiSpawnedCharacters.FirstOrDefaultAsync(s => s.Id == characterId)
            ?? null;
    }


    public static async Task UpdateSpawnedCharacterAsync(ISpawnedCharacter spawnedCharacter)
    {
        await using var db = GetDbContext();

        switch (spawnedCharacter)
        {
            case SakuraAiSpawnedCharacter sakuraAiSpawnedCharacter:
            {
                db.SakuraAiSpawnedCharacters.Update(sakuraAiSpawnedCharacter);
                await db.SaveChangesAsync();
                break;
            }
        }
    }


    public static async Task DeleteSpawnedCharacterAsync(ISpawnedCharacter spawnedCharacter)
    {
        await using var db = GetDbContext();

        switch (spawnedCharacter)
        {
            case SakuraAiSpawnedCharacter sakuraAiSpawnedCharacter:
            {
                db.SakuraAiSpawnedCharacters.Remove(sakuraAiSpawnedCharacter);
                await db.SaveChangesAsync();
                break;
            }
        }
    }


    public static async Task<ISpawnedCharacter> CreateSpawnedCharacterAsync(CommonCharacter commonCharacter, IWebhook webhook)
    {
        var characterName = commonCharacter.CharacterName.Trim();
        if (characterName.Length == 0)
        {
            throw new UserFriendlyException("Invalid character name");
        }

        string callPrefix;
        if (characterName.Contains(' '))
        {
            var split = characterName.Split(' ');
            callPrefix = $"@{split[0][0]}{split[1][0]}".ToLower();
        }
        else if (characterName.Length <= 2)
        {
            callPrefix = $"@{characterName}";
        }
        else
        {
            callPrefix = $"@{characterName[..2]}";
        }

        await using var db = GetDbContext();
        var channel = await db.DiscordChannels
                              .Include(c => c.DiscordGuild)
                              .FirstAsync(c => c.Id == (ulong)webhook.ChannelId!);

        var newSpawnedCharacter = (commonCharacter.GetIntegrationType() switch
        {
            IntegrationType.SakuraAI => new SakuraAiSpawnedCharacter
            {
                DiscordChannelId = channel.Id,
                WebhookId = webhook.Id,
                WebhookToken = webhook.Token,
                CallPrefix = callPrefix,
                MessagesFormat = channel.MessagesFormat ?? channel.DiscordGuild?.MessagesFormat,
                ResponseDelay = 0,
                ResponseChance = 0,
                EnableSwipes = true,
                EnableBuffering = true,
                EnableQuotes = false,
                EnableStopButton = true,
                SkipNextBotMessage = false,
                LastCallerId = 0,
                LastMessageId = 0,
                MessagesSent = 0,
                LastCallTime = default,
                ResetWithNextMessage = true
            }
        }).FillWith(commonCharacter);

        await (newSpawnedCharacter switch
        {
            SakuraAiSpawnedCharacter castedCharacter => db.SakuraAiSpawnedCharacters.AddAsync(castedCharacter),

            _ => throw new ArgumentOutOfRangeException()
        });
        await db.SaveChangesAsync();


        return newSpawnedCharacter;
    }

    #endregion


    #region Integrations

    public static async Task<IGuildIntegration> GetGuildIntegrationAsync(ISpawnedCharacter spawnedCharacter)
    {
        await using var db = GetDbContext();
        var channel = await db.DiscordChannels.FirstAsync(c => c.Id == spawnedCharacter.DiscordChannelId);

        var type = spawnedCharacter.GetIntegrationType();
        IGuildIntegration integration = await (type switch
        {
            IntegrationType.SakuraAI => db.SakuraAiIntegrations.FirstAsync(i => i.DiscordGuildId == channel.DiscordGuildId)
        });

        return integration;
    }


    public static async Task DeleteGuildIntegrationAsync(IGuildIntegration guildIntegration, bool removeCharacters)
    {
        await using var db = GetDbContext();

        switch (guildIntegration)
        {
            case SakuraAiGuildIntegration sakuraAiGuildIntegration:
            {
                if (removeCharacters)
                {
                    var characters = await db.SakuraAiSpawnedCharacters
                                             .Include(character => character.DiscordChannel)
                                             .Where(character => character.DiscordChannel.DiscordGuildId == guildIntegration.DiscordGuildId)
                                             .ToListAsync();

                    db.SakuraAiSpawnedCharacters.RemoveRange(characters);
                }

                db.SakuraAiIntegrations.Remove(sakuraAiGuildIntegration);

                break;
            }
        }

        await db.SaveChangesAsync();
    }



    #endregion


    #region Guilds and channels

    public static async Task EnsureExistInDbAsync(this ITextChannel channel)
    {
        channel.Guild?.EnsureExistInDbAsync().Wait();

        await using var db = GetDbContext();
        var discordChannel = await db.DiscordChannels.FirstOrDefaultAsync(c => c.Id == channel.Id);

        if (discordChannel is null)
        {
            var newChannel = new DiscordChannel
            {
                Id = channel.Id,
                ChannelName = channel.Name,
                DiscordGuildId = channel.GuildId,
                NoWarn = false
            };

            await db.DiscordChannels.AddAsync(newChannel);
        }
        else
        {
            discordChannel.ChannelName = channel.Name;
        }

        await db.SaveChangesAsync();
    }


    public static async Task EnsureExistInDbAsync(this IGuild guild)
    {
        await using var db = GetDbContext();
        var discordGuild = await db.DiscordGuilds.FirstOrDefaultAsync(g => g.Id == guild.Id);

        var owner = (guild as SocketGuild)?.Owner ?? await guild.GetOwnerAsync();
        if (discordGuild is null)
        {
            var newGuild = new DiscordGuild
            {
                Id = guild.Id,
                GuildName = guild.Name,
                MessagesSent = 0,
                OwnerId = owner?.Id ?? guild.OwnerId,
                OwnerUsername = owner?.Username,
                Joined = true,
                FirstJoinDate = DateTime.Now
            };

            await db.DiscordGuilds.AddAsync(newGuild);
        }
        else
        {
            discordGuild.Joined = true;
            discordGuild.GuildName = guild.Name;
            discordGuild.OwnerId = owner?.Id ?? guild.OwnerId;
            discordGuild.OwnerUsername = owner?.Username;
        }

        await db.SaveChangesAsync();
    }


    public static async Task MarkAsLeftAsync(this IGuild guild)
    {
        await guild.EnsureExistInDbAsync();

        await using var db = GetDbContext();
        var discordGuild = await db.DiscordGuilds.FirstAsync(g => g.Id == guild.Id);
        discordGuild.Joined = false;

        await db.SaveChangesAsync();
    }

    #endregion

}
