using CharacterEngine.App.Helpers.Integrations;
using CharacterEngine.App.Helpers.Mappings;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Db;
using CharacterEngineDiscord.Models.Db.Discord;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.Helpers.Common;


public static class DatabaseHelper
{
    public static AppDbContext GetDbContext() => new(BotConfig.DATABASE_CONNECTION_STRING);


    public static async Task<ISpawnedCharacter> GetSpawnedCharacterAsync(DiscordChannelSpawnedCharacter channelCharacter)
    {
        await using var db = GetDbContext();
        var spawnedCharacter = channelCharacter.IntegrationType switch
        {
            IntegrationType.SakuraAI => db.SakuraAiSpawnedCharacters.FindAsync(channelCharacter.SpawnedCharacterId)
        };

        return (await spawnedCharacter as ISpawnedCharacter)!;
    }


    public static async Task<DiscordChannelSpawnedCharacter> LinkToChannelAsync(this ISpawnedCharacter spawnedCharacter, ITextChannel channel)
    {
        await channel.EnsureExistInDbAsync();

        var type = spawnedCharacter.GetIntegrationType();

        var newLink = new DiscordChannelSpawnedCharacter
        {
            Id = Guid.NewGuid(),
            DiscordChannelId = channel.Id,
            SpawnedCharacterId = spawnedCharacter.Id,
            IntegrationType = type,
        };

        await using var db = GetDbContext();
        await db.DiscordChannelSpawnedCharacters.AddAsync(newLink);
        await db.SaveChangesAsync();

        return newLink;
    }


    public static async Task<ISpawnedCharacter> CreateSpawnedCharacterAsync(CommonCharacter commonCharacter, IWebhook webhook)
    {
        var characterName = commonCharacter.Name.Trim();
        if (characterName.Length == 0)
        {
            throw new Exception("Invalid character name");
        }

        string callPrefix;
        if (characterName.Contains(' '))
        {
            var split = characterName.Split(' ');
            callPrefix = $"..{split[0][0]}{split[1][0]}".ToLower();
        }
        else if (characterName.Length == 1)
        {
            callPrefix = $"..{characterName[0]}";
        }
        else
        {
            callPrefix = $"..{characterName[..1]}";
        }

        var newSpawnedCharacter = (commonCharacter.IntegrationType switch
        {
            IntegrationType.SakuraAI => new SakuraAiSpawnedCharacter
            {
                Id = Guid.NewGuid(),
                WebhookId = webhook.Id,
                WebhookToken = webhook.Token,
                CallPrefix = callPrefix,
                MessagesFormat = BotConfig.DEFAULT_MESSAGES_FORMAT,
                ResponseDelay = 0,
                ResponseChance = 0,
                EnableQuotes = false,
                EnableStopButton = true,
                SkipNextBotMessage = false,
                LastCallerId = 0,
                LastMessageId = 0,
                MessagesSent = 0,
                LastCallTime = default
            }
        }).FillWith(commonCharacter);

        await using var db = GetDbContext();
        await (commonCharacter.IntegrationType switch
        {
            IntegrationType.SakuraAI => db.SakuraAiSpawnedCharacters.AddAsync((SakuraAiSpawnedCharacter)newSpawnedCharacter),

        });
        await db.SaveChangesAsync();


        return newSpawnedCharacter;
    }


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
                DiscordGuildId = channel.GuildId
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

}
