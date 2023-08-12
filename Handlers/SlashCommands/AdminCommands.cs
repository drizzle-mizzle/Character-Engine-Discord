using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;
using System;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireAdminAccess]
    [Group("admin", "Admin commands")]
    public class AdminCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly DiscordSocketClient _client;
        private readonly StorageContext _db;

        public AdminCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _db = new StorageContext();
        }

        [SlashCommand("list-servers", "-")]
        public async Task AdminListServers(int page = 1)
        {
            await ListServersAsync(page);
        }

        [SlashCommand("block-server", "-")]
        public async Task AdminBlockGuild(string serverId)
        {
            await BlockGuildAsync(serverId);
        }

        [SlashCommand("block-user-permanent", "-")]
        public async Task AdminBlockUser(string userId)
        {
            await DeferAsync();

            bool ok = ulong.TryParse(userId, out var uUserId);

            if (!ok)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red));
                return;
            }

            if ((await _db.BlockedUsers.FindAsync(uUserId)) is not null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is already blocked".ToInlineEmbed(Color.Red));
                return;
            }

            await _db.BlockedUsers.AddAsync(new() { Id = uUserId, From = DateTime.UtcNow, Hours = 0 });
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("unblock-server", "-")]
        public async Task AdminUnblockGuild(string serverId)
        {
            await DeferAsync();

            var blockedGuild = await _db.BlockedGuilds.FindAsync(ulong.Parse(serverId.Trim()));

            if (blockedGuild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Server not found".ToInlineEmbed(Color.Red));
                return;
            }

            _db.BlockedGuilds.Remove(blockedGuild);
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("unblock-user-permanent", "-")]
        public async Task AdminUnblockUser(string userId)
        {
            await DeferAsync();

            bool ok = ulong.TryParse(userId, out var uUserId);

            if (!ok)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red));
                return;
            }

            var blockedUser = await _db.BlockedUsers.FindAsync(uUserId);
            if (blockedUser is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User not found".ToInlineEmbed(Color.Red));
                return;
            }

            _db.BlockedUsers.Remove(blockedUser);
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("shout-out", "Send a message in each channel where bot was ever called")]
        public async Task AdminShoutOut(string title, string? desc = null, string? imageUrl = null)
        {
            await DeferAsync();

            var embedB = new EmbedBuilder().WithColor(Color.Orange);
            if (title is not null) embedB.WithTitle(title);
            if (desc is not null) embedB.WithDescription(desc);
            if (imageUrl is not null) embedB.WithImageUrl(imageUrl);
            var embed = embedB.Build();

            var channelIds = _db.Channels.Select(c => c.Id).ToList();
            var channels = new List<IMessageChannel>();

            foreach (var channelId in channelIds)
            {
                IMessageChannel? mc;
                try { mc = (await _client.GetChannelAsync(channelId)) as IMessageChannel; }
                catch { continue; }
                if (mc is not null) channels.Add(mc);
            }

            foreach (var channel in channels)
            {
                try { await channel.SendMessageAsync(embed: embed); }
                catch { continue; }
            }
                
            await FollowupAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        [SlashCommand("leave-all-servers", "-")]
        public async Task AdminLeaveAllGuilds()
        {
            await DeferAsync();

            foreach (var guild in _client.Guilds)
            {
                if (guild.Id == Context.Guild.Id) continue;
                await guild.LeaveAsync();
            }

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        [SlashCommand("stats-for-server", "-")]
        public async Task AdminGuildStats(string? guildId = null)
        {
            await DeferAsync();

            ulong uGuildId;
            if (guildId is null)
            {
                uGuildId = Context.Guild.Id;
            }
            else
            {
                if (!ulong.TryParse(guildId, out uGuildId))
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong ID".ToInlineEmbed(Color.Red));
                    return;
                }
            }
            
            var guild = _client.GetGuild(uGuildId);
            var allCharacters = _db.CharacterWebhooks.Where(cw => cw.Channel.GuildId == guild.Id);

            if (allCharacters is null || !allCharacters.Any())
            {
                await FollowupAsync(embed: $"No records".ToInlineEmbed(Color.Orange));
                return;
            }

            int charactersCount = allCharacters.Count();
            bool defOpenAiToken = allCharacters.First().Channel.Guild.GuildOpenAiApiToken is null;
            bool defCaiToken = allCharacters.First().Channel.Guild.GuildCaiUserToken is null;
            DateTime lastUsed = allCharacters.OrderByDescending(c => c.LastCallTime).First().LastCallTime;

            string desc = $"**Owner:** `{guild.Owner.Username}`\n" +
                          $"**Characters:** `{charactersCount}`\n" +
                          $"**Last character call:** `{lastUsed.ToLongDateString()}`\n" +
                          $"**Uses default OpenAI token:** `{defOpenAiToken}`\n" +
                          $"**Uses default cAI token:** `{defCaiToken}`";

            var embed = new EmbedBuilder().WithTitle(guild.Name)
                                          .WithColor(Color.Magenta)
                                          .WithDescription(desc)
                                          .WithImageUrl(guild.IconUrl)
                                          .Build();
            await FollowupAsync(embed: embed);
        }

        [SlashCommand("shutdown", "Shutdown")]
        public async Task AdminShutdownAsync()
        {
            await RespondAsync(embed: $"{WARN_SIGN_DISCORD} Shutting down...".ToInlineEmbed(Color.Orange));

            try { _integration?.CaiClient?.KillBrowser(); }
            catch (Exception e) { LogException(new[] { "Failed to kill Puppeteer processes.\n", e.ToString() }); }
            finally { Environment.Exit(0); }
        }

        [SlashCommand("set-game", "Set game status")]
        public async Task AdminUpdateGame(string? activity = null, string? streamUrl = null, ActivityType type = ActivityType.Playing)
        {
            await _client.SetGameAsync(activity, streamUrl, type);
            await RespondAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        [SlashCommand("set-status", "Set status")]
        public async Task AdminUpdateStatus(UserStatus status)
        {
            await _client.SetStatusAsync(status);
            await RespondAsync(embed: SuccessEmbed(), ephemeral: true);
        }


        ////////////////////
        //// Long stuff ////
        ////////////////////

        private async Task ListServersAsync(int page)
        {
            await DeferAsync();

            var embed = new EmbedBuilder().WithColor(Color.Green);

            int start = (page - 1) * 10;
            int end = (_client.Guilds.Count - start) > 10 ? (start + 9) : start + (_client.Guilds.Count - start - 1);

            var guilds = _client.Guilds.OrderBy(g => g.MemberCount).Reverse();

            for (int i = start; i <= end; i++)
            {
                var guild = guilds.ElementAt(i);
                var guildOwner = await _client.GetUserAsync(guild.OwnerId);
                string val = $"{(guild.Description is string desc ? $"Description: \"{desc}\"\n" : "")}" +
                             $"Owner: {guildOwner?.Username}{(guildOwner?.GlobalName is string gn ? $" ({gn})" : "")}\n" +
                             $"Members: {guild.MemberCount}";
                embed.AddField(guild.Name, val);
            }
            double pages = Math.Ceiling(_client.Guilds.Count / 10d);
            embed.WithTitle($"Servers: {_client.Guilds.Count}");
            embed.WithFooter($"Page {page}/{pages}");

            await FollowupAsync(embed: embed.Build());
        }
        private async Task BlockGuildAsync(string serverId)
        {
            await DeferAsync();

            ulong guildId = ulong.Parse(serverId.Trim());
            var guild = await _db.Guilds.FindAsync(guildId);

            if (guild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Server not found".ToInlineEmbed(Color.Red));
                return;
            }

            if ((await _db.BlockedGuilds.FindAsync(guild.Id)) is not null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Server is aready blocked".ToInlineEmbed(Color.Orange));
                return;
            }

            await _db.BlockedGuilds.AddAsync(new() { Id = guildId });
            _db.Guilds.Remove(guild); // Remove from db
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Server was removed from the database".ToInlineEmbed(Color.Red));

            // Leave
            var discordGuild = _client.GetGuild(guildId);

            if (discordGuild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to leave the server".ToInlineEmbed(Color.Red));
                return;
            }

            await discordGuild.LeaveAsync();
            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Server \"{discordGuild.Name}\" is leaved".ToInlineEmbed(Color.Red));
        }
    }
}
