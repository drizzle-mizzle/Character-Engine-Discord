using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using CharacterEngineDiscord.Domain.Models.Db.Integrations;
using CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;
using CharacterEngineDiscord.Models;
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
        var result = new List<ISpawnedCharacter>();

        await using var db = GetDbContext();
        result.AddRange(await db.SakuraAiSpawnedCharacters.ToListAsync());
        result.AddRange(await db.CaiSpawnedCharacters.ToListAsync());
        // result.AddRange(await db.OpenRouterSpawnedCharacters.ToListAsync());

        return result;
    }


    public static async Task<List<ISpawnedCharacter>> GetAllSpawnedCharactersInChannelAsync(ulong channelId)
    {
        var result = new List<ISpawnedCharacter>();

        await using var db = GetDbContext();
        result.AddRange(await db.SakuraAiSpawnedCharacters.Where(c => c.DiscordChannelId == channelId).ToListAsync());
        result.AddRange(await db.CaiSpawnedCharacters.Where(c => c.DiscordChannelId == channelId).ToListAsync());
        result.AddRange(await db.OpenRouterSpawnedCharacters.Where(c => c.DiscordChannelId == channelId).ToListAsync());

        return result;
    }


    public static async Task<List<ISpawnedCharacter>> GetAllSpawnedCharactersInGuildAsync(ulong guildId)
    {
        var result = new List<ISpawnedCharacter>();

        await using var db = GetDbContext();
        result.AddRange(await db.SakuraAiSpawnedCharacters.Include(c => c.DiscordChannel).Where(c => c.DiscordChannel.DiscordGuildId == guildId).ToListAsync());
        result.AddRange(await db.CaiSpawnedCharacters.Include(c => c.DiscordChannel).Where(c => c.DiscordChannel.DiscordGuildId == guildId).ToListAsync());
        result.AddRange(await db.OpenRouterSpawnedCharacters.Include(c => c.DiscordChannel).Where(c => c.DiscordChannel.DiscordGuildId == guildId).ToListAsync());

        return result;
    }


    public static async Task<ISpawnedCharacter?> GetSpawnedCharacterByIdAsync(Guid characterId)
    {
        await using var db = GetDbContext();

        return await db.SakuraAiSpawnedCharacters.FirstOrDefaultAsync(s => s.Id == characterId) as ISpawnedCharacter
            ?? await db.CaiSpawnedCharacters.FirstOrDefaultAsync(c => c.Id == characterId) as ISpawnedCharacter
            ?? await db.OpenRouterSpawnedCharacters.FirstOrDefaultAsync(o => o.Id == characterId) as ISpawnedCharacter;
    }


    public static async Task UpdateSpawnedCharacterAsync(ISpawnedCharacter spawnedCharacter)
    {
        await using var db = GetDbContext();

        switch (spawnedCharacter)
        {
            case SakuraAiSpawnedCharacter sakuraAiSpawnedCharacter:
            {
                db.SakuraAiSpawnedCharacters.Update(sakuraAiSpawnedCharacter);
                break;
            }
            case CaiSpawnedCharacter caiSpawnedCharacter:
            {
                db.CaiSpawnedCharacters.Update(caiSpawnedCharacter);
                break;
            }
            case OpenRouterSpawnedCharacter openRouterSpawnedCharacter:
            {
                db.OpenRouterSpawnedCharacters.Update(openRouterSpawnedCharacter);
                break;
            }
        }

        await db.SaveChangesAsync();
    }


    public static async Task DeleteSpawnedCharacterAsync(ISpawnedCharacter spawnedCharacter)
    {
        await using var db = GetDbContext();

        var characterExist = await (spawnedCharacter.GetIntegrationType() switch
        {
            IntegrationType.SakuraAI => db.SakuraAiSpawnedCharacters.AnyAsync(c => c.Id == spawnedCharacter.Id),
            IntegrationType.CharacterAI => db.CaiSpawnedCharacters.AnyAsync(c => c.Id == spawnedCharacter.Id),
            IntegrationType.OpenRouter => db.OpenRouterSpawnedCharacters.AnyAsync(c => c.Id == spawnedCharacter.Id),
        });

        if (!characterExist)
        {
            return;
        }

        switch (spawnedCharacter)
        {
            case SakuraAiSpawnedCharacter sakuraAiSpawnedCharacter:
            {
                db.SakuraAiSpawnedCharacters.Remove(sakuraAiSpawnedCharacter);
                break;
            }
            case CaiSpawnedCharacter caiSpawnedCharacter:
            {
                db.CaiSpawnedCharacters.Remove(caiSpawnedCharacter);
                break;
            }
            case OpenRouterSpawnedCharacter openRouterSpawnedCharacter:
            {
                db.OpenRouterSpawnedCharacters.Remove(openRouterSpawnedCharacter);
                break;
            }
        }

        await db.SaveChangesAsync();
    }



    // public static async Task DeleteSpawnedCharactersAsync(ICollection<ISpawnedCharacter> spawnedCharacters)
    // {
    //     if (spawnedCharacters.Count == 0)
    //     {
    //         return;
    //     }
    //
    //     await using var db = GetDbContext();
    //
    //     switch (spawnedCharacters)
    //     {
    //         case ICollection<SakuraAiSpawnedCharacter> sakuraAiSpawnedCharacters:
    //         {
    //             db.SakuraAiSpawnedCharacters.RemoveRange(sakuraAiSpawnedCharacters);
    //             break;
    //         }
    //         case ICollection<CaiSpawnedCharacter> caiSpawnedCharacters:
    //         {
    //             db.CaiSpawnedCharacters.RemoveRange(caiSpawnedCharacters);
    //             break;
    //         }
    //         case ICollection<OpenRouterSpawnedCharacter> openRouterSpawnedCharacters:
    //         {
    //             db.OpenRouterSpawnedCharacters.RemoveRange(openRouterSpawnedCharacters);
    //             break;
    //         }
    //     }
    //
    //     await db.SaveChangesAsync();
    // }


    #endregion


    #region Integrations

    public static async Task<IGuildIntegration?> GetGuildIntegrationAsync(Guid integrationId)
    {
        await using var db = GetDbContext();

        return await db.SakuraAiIntegrations.FirstOrDefaultAsync(s => s.Id == integrationId) as IGuildIntegration
            ?? await db.CaiIntegrations.FirstOrDefaultAsync(c => c.Id == integrationId) as IGuildIntegration
            ?? await db.OpenRouterIntegrations.FirstOrDefaultAsync(c => c.Id == integrationId) as IGuildIntegration;
    }


    public static async Task<IGuildIntegration?> GetGuildIntegrationAsync(ISpawnedCharacter spawnedCharacter)
    {
        await using var db = GetDbContext();
        var guildId = await db.DiscordChannels
                              .Where(c => c.Id == spawnedCharacter.DiscordChannelId)
                              .Select(c => c.DiscordGuildId)
                              .FirstAsync();

        var type = spawnedCharacter.GetIntegrationType();
        IGuildIntegration? integration = type switch
        {
            IntegrationType.SakuraAI => await db.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
            IntegrationType.CharacterAI => await db.CaiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
            IntegrationType.OpenRouter => await db.OpenRouterIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
        };

        return integration;
    }


    public static async Task<IGuildIntegration?> GetGuildIntegrationAsync(ulong guildId, IntegrationType integrationType)
    {
        await using var db = GetDbContext();

        IGuildIntegration? integration = integrationType switch
        {
            IntegrationType.SakuraAI => await db.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
            IntegrationType.CharacterAI => await db.CaiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
            IntegrationType.OpenRouter => await db.OpenRouterIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
        };

        return integration;
    }


    public static async Task<List<IGuildIntegration>> GetAllIntegrationsInGuildAsync(ulong guildId)
    {
        var result = new List<IGuildIntegration>();

        await using var db = GetDbContext();

        var sakuraAiGuildIntegration = await db.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId);
        if (sakuraAiGuildIntegration is not null)
        {
            result.Add(sakuraAiGuildIntegration);
        }

        var characterAiGuildIntegration = await db.CaiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId);
        if (characterAiGuildIntegration is not null)
        {
            result.Add(characterAiGuildIntegration);
        }

        var openrouterGuildIntegration = await db.OpenRouterIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId);
        if (openrouterGuildIntegration is not null)
        {
            result.Add(openrouterGuildIntegration);
        }

        return result;
    }


    public static async Task DeleteGuildIntegrationAsync(IGuildIntegration guildIntegration)
    {
        await using var db = GetDbContext();

        switch (guildIntegration)
        {
            case SakuraAiGuildIntegration sakuraAiGuildIntegration:
            {
                db.SakuraAiIntegrations.Remove(sakuraAiGuildIntegration);
                break;
            }
            case CaiGuildIntegration caiGuildIntegration:
            {
                db.CaiIntegrations.Remove(caiGuildIntegration);
                break;
            }
            case OpenRouterGuildIntegration openRouterGuildIntegration:
            {
                db.OpenRouterIntegrations.Remove(openRouterGuildIntegration);
                break;
            }
        }

        await db.SaveChangesAsync();
    }


    #endregion


    public static void EnsureCached(this IGuildUser guildUser)
    {
        if (!MemoryStorage.CachedUsers.TryAdd(guildUser.Id, null))
        {
            return;
        }

        Task.Run(async () =>
        {
            guildUser.Guild.EnsureCached(wait: true);

            await using var db = GetDbContext();

            db.DiscordUsers.Add(new DiscordUser
            {
                Id = guildUser.Id
            });

            await db.SaveChangesAsync();
        });
    }

    public static void EnsureCached(this IGuildChannel channel)
    {
        if (!MemoryStorage.CachedChannels.TryAdd(channel.Id, false))
        {
            return;
        }

        Task.Run(async () =>
        {
            channel.Guild?.EnsureCached(wait: true);

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

                db.DiscordChannels.Add(newChannel);
                await db.SaveChangesAsync();
            }
            else
            {
                MemoryStorage.CachedChannels[channel.Id] = discordChannel.NoWarn;
                if (discordChannel.ChannelName != channel.Name)
                {
                    discordChannel.ChannelName = channel.Name;
                    await db.SaveChangesAsync();
                }
            }
        });
    }


    public static void EnsureCached(this IGuild guild, bool wait = false)
    {
        if (!MemoryStorage.CachedGuilds.TryAdd(guild.Id, null))
        {
            return;
        }

        var task = Task.Run(async () =>
        {
            await using var db = GetDbContext();
            var discordGuild = await db.DiscordGuilds.FirstOrDefaultAsync(g => g.Id == guild.Id);

            string? ownerUsername = null;
            int? memberCount = null;
            if (guild is SocketGuild socketGuild)
            {
                ownerUsername = socketGuild.Owner?.Username;
                memberCount = socketGuild.MemberCount;
            }

            ownerUsername ??= (await guild.GetOwnerAsync())?.Username;
            memberCount ??= guild.ApproximateMemberCount;

            if (discordGuild is null)
            {
                var newGuild = new DiscordGuild
                {
                    Id = guild.Id,
                    GuildName = guild.Name,
                    MessagesSent = 0,
                    NoWarn = false,
                    OwnerId = guild.OwnerId,
                    OwnerUsername = ownerUsername,
                    MemberCount = memberCount ?? 0,
                    Joined = true,
                    FirstJoinDate = DateTime.Now
                };

                db.DiscordGuilds.Add(newGuild);
            }
            else
            {
                discordGuild.Joined = true;
                discordGuild.GuildName = guild.Name ?? "";
                discordGuild.OwnerId = guild.OwnerId;
                discordGuild.OwnerUsername = ownerUsername;
                discordGuild.MemberCount = memberCount ?? 0;
            }

            await db.SaveChangesAsync();
        });

        if (wait)
        {
            task.Wait();
        }
    }



    public static async Task MarkAsLeftAsync(this IGuild guild)
    {
        guild.EnsureCached();

        await using var db = GetDbContext();
        var discordGuild = await db.DiscordGuilds.FirstAsync(g => g.Id == guild.Id);
        discordGuild.Joined = false;

        await db.SaveChangesAsync();
    }
}
