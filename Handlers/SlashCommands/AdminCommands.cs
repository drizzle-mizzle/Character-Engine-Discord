using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;
using System.Diagnostics;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireHosterAccess]
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


        [SlashCommand("status", "-")]
        public async Task AdminStatus(bool silent = true)
        {
            await DeferAsync(ephemeral: silent);
            var time = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            string text = $"Running: `{time.Days}d/{time.Hours}h/{time.Minutes}m`\n" +
                          $"Blocked: `{_db.BlockedUsers.Where(bu => bu.GuildId == null).Count()} user(s)` | `{_db.BlockedGuilds.Count()} guild(s)`\n" +
                          $"Stats: `{_integration.WebhookClients.Count}wc/{_integration.SearchQueries.Count}sq`";

            await FollowupAsync(embed: text.ToInlineEmbed(Color.Green, false), ephemeral: silent);
        }


        [SlashCommand("list-servers", "-")]
        public async Task AdminListServers(int page = 1, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            var embed = new EmbedBuilder().WithColor(Color.Green);

            int start = (page - 1) * 10;
            int end = (_client.Guilds.Count - start) > 10 ? (start + 9) : start + (_client.Guilds.Count - start - 1);

            var guilds = _client.Guilds.OrderBy(g => g.MemberCount).Reverse();

            for (int i = start; i <= end; i++)
            {
                var guild = guilds.ElementAt(i);
                var guildOwner = await _client.GetUserAsync(guild.OwnerId);
                string val = $"ID: {guild.Id}\n" +
                             $"{(guild.Description is string desc ? $"Description: \"{desc[0..Math.Min(200, desc.Length - 1)]}\"\n" : "")}" +
                             $"Owner: {guildOwner?.Username}{(guildOwner?.GlobalName is string gn ? $" ({gn})" : "")}\n" +
                             $"Members: {guild.MemberCount}";
                embed.AddField(guild.Name, val);
            }
            double pages = Math.Ceiling(_client.Guilds.Count / 10d);
            embed.WithTitle($"Servers: {_client.Guilds.Count}");
            embed.WithFooter($"Page {page}/{pages}");

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }


        [SlashCommand("leave-all-servers", "-")]
        public async Task AdminLeaveAllGuilds(bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            var guilds = _client.Guilds.Where(g => g.Id != Context.Guild.Id);
            await Parallel.ForEachAsync(guilds, async (guild, ct) => await guild.LeaveAsync());

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("block-server", "-")]
        public async Task AdminBlockGuild(string serverId, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            ulong guildId = ulong.Parse(serverId.Trim());
            var guild = await _db.Guilds.FindAsync(guildId);

            if (guild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Server not found".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            if ((await _db.BlockedGuilds.FindAsync(guild.Id)) is not null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Server is aready blocked".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            await _db.BlockedGuilds.AddAsync(new() { Id = guildId });
            _db.Guilds.Remove(guild); // Remove from db
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Server was removed from the database".ToInlineEmbed(Color.Red), ephemeral: silent);

            // Leave
            var discordGuild = _client.GetGuild(guildId);

            if (discordGuild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to leave the server".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await discordGuild.LeaveAsync();
            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Server \"{discordGuild.Name}\" is leaved".ToInlineEmbed(Color.Red), ephemeral: silent);
        }


        [SlashCommand("unblock-server", "-")]
        public async Task AdminUnblockGuild(string serverId, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            var blockedGuild = await _db.BlockedGuilds.FindAsync(ulong.Parse(serverId.Trim()));

            if (blockedGuild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Server not found".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            _db.BlockedGuilds.Remove(blockedGuild);
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("block-user-global", "-")]
        public async Task AdminBlockUser(string userId, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            bool ok = ulong.TryParse(userId, out var uUserId);

            if (!ok)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            if ((await _db.BlockedUsers.FindAsync(uUserId)) is not null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is already blocked".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await _db.BlockedUsers.AddAsync(new() { Id = uUserId, From = DateTime.UtcNow, Hours = 0 });
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("unblock-user-global", "-")]
        public async Task AdminUnblockUser(string userId, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            bool ok = ulong.TryParse(userId, out var uUserId);

            if (!ok)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            var blockedUser = await _db.BlockedUsers.FindAsync(uUserId);
            if (blockedUser is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User not found".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            _db.BlockedUsers.Remove(blockedUser);
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("broadcast", "Send a message in each channel where bot was ever called")]
        public async Task AdminShoutOut(string title, string? desc = null, string? imageUrl = null, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            var embedB = new EmbedBuilder().WithColor(Color.Orange);
            if (title is not null) embedB.WithTitle(title);
            if (desc is not null) embedB.WithDescription(desc);
            if (imageUrl is not null) embedB.WithImageUrl(imageUrl);
            var embed = embedB.Build();

            var channelIds = _db.Channels.Select(c => c.Id).ToList();
            var channels = new List<IMessageChannel>();

            await Parallel.ForEachAsync(channelIds, async (channelId, ct) =>
            {
                IMessageChannel? mc;
                try { mc = (await _client.GetChannelAsync(channelId)) as IMessageChannel; }
                catch { return; }
                if (mc is not null) channels.Add(mc);
            });

            int count = 0;
            await Parallel.ForEachAsync(channels, async (channel, ct) =>
            {
                try {
                    await channel.SendMessageAsync(embed: embed);
                    count++;
                }
                catch { return; }
            });
                
            await FollowupAsync(embed: SuccessEmbed($"Message was sent in {count} channels"), ephemeral: silent);
        }


        [SlashCommand("server-stats", "-")]
        public async Task AdminGuildStats(string? guildId = null, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            ulong uGuildId;
            if (guildId is null)
            {
                uGuildId = Context.Guild.Id;
            }
            else
            {
                if (!ulong.TryParse(guildId, out uGuildId))
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong ID".ToInlineEmbed(Color.Red), ephemeral: silent);
                    return;
                }
            }
            
            var guild = _client.GetGuild(uGuildId);
            if (guild is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Guild not found".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            var dbGuild = await FindOrStartTrackingGuildAsync(guild.Id, _db);
            var allCharacters = _db.CharacterWebhooks.Where(cw => cw.Channel.GuildId == guild.Id);

            if (allCharacters is null || !allCharacters.Any())
            {
                await FollowupAsync(embed: $"No records ({dbGuild.MessagesSent})".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            int charactersCount = allCharacters.Count();
            var lastUsed = allCharacters.OrderByDescending(c => c.LastCallTime).First().LastCallTime;
            string callDate = $"{lastUsed.Day}/{lastUsed.Month}/{lastUsed.Year}";
            
            string desc = $"**Owner:** `{guild.Owner?.Username}`\n" +
                          $"**Characters:** `{charactersCount}`\n" +
                          $"**Last character call:** `{callDate}`\n" +
                          $"**Messages sent:** `{dbGuild.MessagesSent}`";

            var embed = new EmbedBuilder().WithTitle(guild.Name)
                                          .WithColor(Color.Magenta)
                                          .WithDescription(desc);
            try
            {
                string iconUrl = guild.IconUrl;
                if (!string.IsNullOrWhiteSpace(iconUrl))
                    embed.WithImageUrl(iconUrl);
            }
            finally
            {
                await FollowupAsync(embed: embed.Build(), ephemeral: silent);
            }
        }


        [SlashCommand("shutdown", "Shutdown")]
        public async Task AdminShutdownAsync(bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            if (_integration.CaiClient is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Puppeteer is not launched".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Shutting down...".ToInlineEmbed(Color.Orange), ephemeral: silent);

            try { _integration.CaiClient.KillBrowser(); }
            catch (Exception e)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to kill Puppeteer processes".ToInlineEmbed(Color.Red), ephemeral: silent);
                LogException("Failed to kill Puppeteer processes.\n", e);
                return;
            }

            Environment.Exit(0);
        }


        [SlashCommand("relaunch-puppeteer", "-")]
        public async Task RelaunchBrowser(bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            if (_integration.CaiClient is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Puppeteer is not launched".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Shutting Puppeteer down...".ToInlineEmbed(Color.LightOrange), ephemeral: silent);

            try { _integration.CaiClient.KillBrowser(); }
            catch (Exception e)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to kill Puppeteer processes".ToInlineEmbed(Color.Red), ephemeral: silent);
                LogException("Failed to kill Puppeteer processes", e);
                return;
            }

            await FollowupAsync(embed: "Launching Puppeteer...".ToInlineEmbed(Color.Purple), ephemeral: silent);

            try { _integration.CaiClient.LaunchBrowser(killDuplicates: true); }
            catch (Exception e)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to launch Puppeteer processes".ToInlineEmbed(Color.Red));
                LogException("Failed to launch Puppeteer processes", e);
                return;
            }

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: silent);
        }


        [SlashCommand("set-game", "Set game status")]
        public async Task AdminUpdateGame(string? activity = null, string? streamUrl = null, ActivityType type = ActivityType.Playing, bool silent = true)
        {
            await _client.SetGameAsync(activity, streamUrl, type);
            await RespondAsync(embed: SuccessEmbed(), ephemeral: silent);

            string gamePath = $"{EXE_DIR}{SC}storage{SC}lastgame.txt";
            File.WriteAllText(gamePath, activity ?? "");
        }


        [SlashCommand("set-status", "Set status")]
        public async Task AdminUpdateStatus(UserStatus status, bool silent = true)
        {
            await _client.SetStatusAsync(status);
            await RespondAsync(embed: SuccessEmbed(), ephemeral: silent);
        }
    }
}
