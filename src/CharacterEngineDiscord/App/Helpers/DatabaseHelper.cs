using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Infrastructure;
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
        var result = new List<ISpawnedCharacter>();

        await using var db = GetDbContext();
        result.AddRange(await db.SakuraAiSpawnedCharacters.ToListAsync());
        result.AddRange(await db.CaiSpawnedCharacters.ToListAsync());

        return result;
    }


    public static async Task<List<ISpawnedCharacter>> GetAllSpawnedCharactersInChannelAsync(ulong channelId)
    {
        var result = new List<ISpawnedCharacter>();

        await using var db = GetDbContext();
        result.AddRange(await db.SakuraAiSpawnedCharacters.Where(c => c.DiscordChannelId == channelId).ToListAsync());
        result.AddRange(await db.CaiSpawnedCharacters.Where(c => c.DiscordChannelId == channelId).ToListAsync());

        return result;
    }


    public static async Task<List<ISpawnedCharacter>> GetAllSpawnedCharactersInGuildAsync(ulong guildId)
    {
        var result = new List<ISpawnedCharacter>();

        await using var db = GetDbContext();
        result.AddRange(await db.SakuraAiSpawnedCharacters.Include(c => c.DiscordChannel).Where(c => c.DiscordChannel!.DiscordGuildId == guildId).ToListAsync());
        result.AddRange(await db.CaiSpawnedCharacters.Include(c => c.DiscordChannel).Where(c => c.DiscordChannel!.DiscordGuildId == guildId).ToListAsync());

        return result;
    }


    public static async Task<ISpawnedCharacter?> GetSpawnedCharacterByIdAsync(Guid characterId)
    {
        await using var db = GetDbContext();

        return await db.SakuraAiSpawnedCharacters.FirstOrDefaultAsync(s => s.Id == characterId) as ISpawnedCharacter
            ?? await db.CaiSpawnedCharacters.FirstOrDefaultAsync(c => c.Id == characterId) as ISpawnedCharacter
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
                break;
            }
            case CaiSpawnedCharacter caiSpawnedCharacter:
            {
                db.CaiSpawnedCharacters.Update(caiSpawnedCharacter);
                break;
            }
        }

        await db.SaveChangesAsync();
    }


    public static async Task DeleteSpawnedCharacterAsync(ISpawnedCharacter spawnedCharacter)
    {
        await using var db = GetDbContext();

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
        }

        await db.SaveChangesAsync();
    }


    public static async Task DeleteSpawnedCharactersAsync(ICollection<ISpawnedCharacter> spawnedCharacters)
    {
        if (spawnedCharacters.Count == 0)
        {
            return;
        }

        await using var db = GetDbContext();

        switch (spawnedCharacters)
        {
            case ICollection<SakuraAiSpawnedCharacter> sakuraAiSpawnedCharacters:
            {
                db.SakuraAiSpawnedCharacters.RemoveRange(sakuraAiSpawnedCharacters);
                break;
            }
            case ICollection<CaiSpawnedCharacter> caiSpawnedCharacters:
            {
                db.CaiSpawnedCharacters.RemoveRange(caiSpawnedCharacters);
                break;
            }
        }

        await db.SaveChangesAsync();
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
            var split = characterName.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            callPrefix = $"@{split[0][0]}{split[1][0]}";
        }
        else if (characterName.Length > 2)
        {
            callPrefix = $"@{characterName[..2]}";
        }
        else
        {
            callPrefix = $"@{characterName}";
        }

        await using var db = GetDbContext();
        var channel = await db.DiscordChannels
                              .Include(c => c.DiscordGuild)
                              .FirstAsync(c => c.Id == (ulong)webhook.ChannelId!);

        ISpawnedCharacter newSpawnedCharacter = commonCharacter.IntegrationType switch
        {
            IntegrationType.SakuraAI => new SakuraAiSpawnedCharacter(),
            IntegrationType.CharacterAI => new CaiSpawnedCharacter(),
        };

        newSpawnedCharacter.CharacterId = commonCharacter.CharacterId;
        newSpawnedCharacter.CharacterName = commonCharacter.CharacterName;
        newSpawnedCharacter.CharacterFirstMessage = commonCharacter.CharacterFirstMessage;
        newSpawnedCharacter.CharacterImageLink = commonCharacter.CharacterImageLink;
        newSpawnedCharacter.CharacterAuthor = commonCharacter.CharacterAuthor ?? "unknown";
        newSpawnedCharacter.IsNfsw = commonCharacter.IsNfsw;
        newSpawnedCharacter.DiscordChannelId = channel.Id;
        newSpawnedCharacter.WebhookId = webhook.Id;
        newSpawnedCharacter.WebhookToken = webhook.Token;
        newSpawnedCharacter.CallPrefix = callPrefix.ToLower();
        newSpawnedCharacter.MessagesFormat = channel.MessagesFormat ?? channel.DiscordGuild?.MessagesFormat;
        newSpawnedCharacter.ResponseDelay = 1;
        newSpawnedCharacter.FreewillFactor = 0;
        newSpawnedCharacter.EnableSwipes = false;
        newSpawnedCharacter.EnableWideContext = true;
        newSpawnedCharacter.WideContextMaxLength = 1000;
        newSpawnedCharacter.EnableQuotes = false;
        newSpawnedCharacter.EnableStopButton = true;
        newSpawnedCharacter.SkipNextBotMessage = false;
        newSpawnedCharacter.LastCallerDiscordUserId = 0;
        newSpawnedCharacter.LastDiscordMessageId = 0;
        newSpawnedCharacter.MessagesSent = 0;
        newSpawnedCharacter.LastCallTime = default;
        newSpawnedCharacter.ResetWithNextMessage = true;

        switch (newSpawnedCharacter)
        {
            case SakuraAiSpawnedCharacter sakuraAiSpawnedCharacter:
            {
                await db.SakuraAiSpawnedCharacters.AddAsync(sakuraAiSpawnedCharacter);
                break;
            }
            case CaiSpawnedCharacter caiSpawnedCharacter:
            {
                await db.CaiSpawnedCharacters.AddAsync(caiSpawnedCharacter);
                break;
            }
        }

        await db.SaveChangesAsync();

        return newSpawnedCharacter;
    }

    #endregion


    #region Integrations

    public static async Task<IGuildIntegration?> GetGuildIntegrationAsync(Guid integrationId)
    {
        await using var db = GetDbContext();

        return await db.SakuraAiIntegrations.FirstOrDefaultAsync(s => s.Id == integrationId) as IGuildIntegration
            ?? await db.CaiIntegrations.FirstOrDefaultAsync(c => c.Id == integrationId) as IGuildIntegration
            ?? null;
    }


    public static async Task<IGuildIntegration?> GetGuildIntegrationAsync(ISpawnedCharacter spawnedCharacter)
    {
        await using var db = GetDbContext();
        var channel = await db.DiscordChannels.FirstAsync(c => c.Id == spawnedCharacter.DiscordChannelId);

        var type = spawnedCharacter.GetIntegrationType();
        IGuildIntegration? integration = type switch
        {
            IntegrationType.SakuraAI => await db.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == channel.DiscordGuildId),
            IntegrationType.CharacterAI => await db.CaiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == channel.DiscordGuildId),
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

        return result;
    }


    public static async Task DeleteGuildIntegrationAsync(IGuildIntegration guildIntegration, bool removeCharacters)
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
        }

        await db.SaveChangesAsync();

        var giIntegrationType = guildIntegration.GetIntegrationType();

        var guildCharacters = await GetAllSpawnedCharactersInGuildAsync(guildIntegration.DiscordGuildId);
        var neededCharacters = guildCharacters.Where(character => character.GetIntegrationType() == giIntegrationType).ToArray();

        var tasks = new List<Task>();

        if (removeCharacters)
        {
            tasks.Add(DeleteSpawnedCharactersAsync(neededCharacters));
        }
        else
        {
            foreach (var character in neededCharacters)
            {
                character.FreewillFactor = 0;
                tasks.Add(UpdateSpawnedCharacterAsync(character));
            }
        }

        Task.WaitAll(tasks.ToArray());
    }


    #endregion


    #region Guilds and channels

    public static async Task EnsureExistInDbAsync(this IGuildChannel channel)
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
            await db.SaveChangesAsync();
        }
        else if (discordChannel.ChannelName != channel.Name)
        {
            discordChannel.ChannelName = channel.Name;
            await db.SaveChangesAsync();
        }
    }


    public static async Task EnsureExistInDbAsync(this IGuild guild)
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

            await db.DiscordGuilds.AddAsync(newGuild);
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
