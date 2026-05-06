using System.Collections.Concurrent;
using System.Collections.Immutable;
using CharacterEngine.App.Repositories.Abstractions;
using CharacterEngine.App.Repositories.Storages;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.WebSocket;

namespace CharacterEngine.App.Repositories;


public class CacheRepository : RepositoryBase
{
    /// <summary>
    /// ChannelId : NoWarn
    /// </summary>
    private static readonly ConcurrentDictionary<ulong, (bool NoWarn, DateTime LastHitAt)> _cachedChannels = [];

    private static readonly ConcurrentDictionary<ulong, DateTime> _cachedGuilds = [];

    private static readonly ConcurrentDictionary<ulong, DateTime> _cachedUsers = [];


    // TODO: rework to human look
    public ImmutableDictionary<ulong, (bool NoWarn, DateTime CachedAt)> GetAllCachedChannels => _cachedChannels.ToImmutableDictionary();
    public ImmutableDictionary<ulong, DateTime> GetAllCachedGuilds => _cachedGuilds.ToImmutableDictionary();
    public ImmutableDictionary<ulong, DateTime> GetAllCachedUsers => _cachedUsers.ToImmutableDictionary();

    public void RemoveCachedChannel(ulong channelId)
        => _cachedChannels.TryRemove(channelId, out _);

    public void RemoveCachedGuild(ulong guildId)
        => _cachedGuilds.TryRemove(guildId, out _);

    public void RemoveCachedUser(ulong userId)
        => _cachedUsers.TryRemove(userId, out _);

    public static bool GetCachedChannelNoWarnState(ulong channelId)
        => _cachedChannels.TryGetValue(channelId, out var channel) && channel.NoWarn;



    public CachedCharacerInfoStorage CachedCharacters { get; } = new();

    public CachedWebhookClientsStorage CachedWebhookClients { get; } = new();

    public ActiveSearchQueriesStorage ActiveSearchQueries { get; } = new();


    private readonly SemaphoreSlim _dbCallsSemaphore = new(1, 1);

    public CacheRepository(AppDbContext db) : base(db) { }


    public void CacheUser(ulong userId)
    {
        _cachedUsers.TryAdd(userId, DateTime.Now);
    }


    public Task EnsureUserCached(IGuildUser guildUser)
    {
        if (!_cachedUsers.TryAdd(guildUser.Id, DateTime.Now))
        {
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            await EnsureGuildCached(guildUser.Guild);

            await _dbCallsSemaphore.WaitAsync();
            try
            {
                var discordUser = await DB.DiscordUsers.FindAsync(guildUser.Id);

                if (discordUser is null)
                {
                    DB.DiscordUsers.Add(new DiscordUser
                    {
                        Id = guildUser.Id
                    });

                    await DB.SaveChangesAsync();
                }

                _cachedUsers[guildUser.Id] = DateTime.Now;
            }
            finally
            {
                _dbCallsSemaphore.Release();
            }
        });
    }

    public Task EnsureChannelCached(IGuildChannel channel)
    {
        if (!_cachedChannels.TryAdd(channel.Id, (false, DateTime.Now)))
        {
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            await EnsureGuildCached(channel.Guild);

            await _dbCallsSemaphore.WaitAsync();
            try
            {
                var discordChannel = await DB.DiscordChannels.FindAsync(channel.Id);
                if (discordChannel is null)
                {
                    discordChannel = new DiscordChannel
                    {
                        Id = channel.Id,
                        ChannelName = channel.Name,
                        DiscordGuildId = channel.GuildId,
                        NoWarn = false
                    };

                    DB.DiscordChannels.Add(discordChannel);
                }
                else if (discordChannel.ChannelName != channel.Name)
                {
                    discordChannel.ChannelName = channel.Name;
                }

                await DB.SaveChangesAsync();

                _cachedChannels[channel.Id] = (discordChannel.NoWarn, DateTime.Now);
            }
            finally
            {
                _dbCallsSemaphore.Release();
            }
        });
    }


    public Task EnsureGuildCached(IGuild guild)
    {
        if (!_cachedGuilds.TryAdd(guild.Id, DateTime.Now))
        {
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            await _dbCallsSemaphore.WaitAsync();
            try
            {
                var discordGuild = await DB.DiscordGuilds.FindAsync(guild.Id);

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
                    discordGuild = new DiscordGuild
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

                    DB.DiscordGuilds.Add(discordGuild);
                }
                else
                {
                    discordGuild.Joined = true;
                    discordGuild.GuildName = guild.Name ?? "";
                    discordGuild.OwnerId = guild.OwnerId;
                    discordGuild.OwnerUsername = ownerUsername;
                    discordGuild.MemberCount = memberCount ?? 0;
                }

                await DB.SaveChangesAsync();

                _cachedGuilds[guild.Id] = DateTime.Now;
            }
            finally
            {
                _dbCallsSemaphore.Release();
            }
        });
    }
}
