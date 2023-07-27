using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;
using PuppeteerSharp;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [DefaultMemberPermissions(GuildPermission.Administrator)]
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
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("list-servers", "-")]
        public async Task ListServers(int page = 1)
        {
            var embed = new EmbedBuilder().WithColor(Color.Green);

            int start = (page - 1) * 10;
            int end = start + 9;
            if ((start + end + 1) > _client.Guilds.Count)
                end = _client.Guilds.Count - start - 1;

            foreach (var guild in _client.Guilds)
            {
                string val = $"{(guild.Description is string desc ? $"Description: \"{desc}\"\n" : "")}" +
                             $"Owner: {guild.Owner.Username}{(guild.Owner.DisplayName is string dn ? $" ({dn})" : "")}\n" +
                             $"Members: {guild.MemberCount}";
                embed.AddField(guild.Name, val);
            }
            double pages = Math.Ceiling(_client.Guilds.Count / 10d);
            embed.WithTitle($"Page {page}/{pages}\nServers: {_client.Guilds.Count}");

            await RespondAsync(embed: embed.Build());
        }

        [SlashCommand("block-server", "-")]
        public async Task BlockGuild(string serverId)
        {
            await BlockGuildAsync(serverId);
        }

        [SlashCommand("unblock-server", "-")]
        public async Task UnblockGuild(string serverId)
        {
            await DeferAsync();

            var blockedGuild = await _db.BlockedGuilds.FindAsync(ulong.Parse(serverId));

            if (blockedGuild is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Server not found", Color.Red));
                return;
            }

            _db.BlockedGuilds.Remove(blockedGuild);
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("block-user", "-")]
        public async Task BlockUser(string sUserId)
        {
            await DeferAsync();

            ulong userId = ulong.Parse(sUserId);
            if ((await _db.BlockedUsers.FindAsync(userId)) is not null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} User is already blocked", Color.Red));
                return;
            }

            await _db.BlockedUsers.AddAsync(new() { Id = userId });
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("unblock-user", "-")]
        public async Task UnblockUser(string sUserId)
        {
            await DeferAsync();

            ulong userId = ulong.Parse(sUserId);
            var blockedUser = await _db.BlockedUsers.FindAsync(userId);
            if (blockedUser is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} User not found", Color.Red));
                return;
            }

            _db.BlockedUsers.Remove(blockedUser);
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("shout-out", "Send a message in each channel where bot was ever called")]
        public async Task ShoutOut(string title, string? desc = null, string? imageUrl = null)
        {
            await DeferAsync();

            var embed = new EmbedBuilder().WithColor(Color.Orange);
            if (title is not null) embed.WithTitle(title);
            if (desc is not null) embed.WithDescription(desc);
            if (imageUrl is not null) embed.WithImageUrl(imageUrl);

            var channelIds = _db.Channels.Select(c => c.Id).ToList();
            var channels = new List<IMessageChannel>();

            foreach (var channelId in channelIds)
            {
                var sgc = (await _client.GetChannelAsync(channelId)) as IMessageChannel;
                if (sgc is not null) channels.Add(sgc);
            }

            foreach (var channel in channels)
                await channel.SendMessageAsync(embed: embed.Build());

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        [SlashCommand("leave-all-servers", "-")]
        public async Task LeaveAllGuilds()
        {
            await DeferAsync();

            foreach (var guild in _client.Guilds)
            {
                if (guild.Id == Context.Guild.Id) continue;
                await guild.LeaveAsync();
            }

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: true);
        }


        [SlashCommand("shutdown", "Shutdown")]
        public async Task ShutdownAsync()
        {
            await RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Shutting down...", Color.Orange));
            try { _integration?.CaiClient?.KillBrowser(); }
            catch (Exception e) { LogException(new[] { "Failed to kill Puppeteer processes.\n", e.ToString() }); }
            Environment.Exit(0);   
        }

        [SlashCommand("set-game", "Set game status")]
        public async Task UpdateGame(string? activity = null, string? streamUrl = null, ActivityType type = ActivityType.Playing)
            => await _client.SetGameAsync(activity, streamUrl, type);

        [SlashCommand("set-status", "Set status")]
        public async Task UpdateStatus(UserStatus status)
            => await _client.SetStatusAsync(status);

        [SlashCommand("ping", "ping")]
        public async Task Ping()
            => await RespondAsync(embed: InlineEmbed($":ping_pong: Pong! - {_client.Latency} ms", Color.Red));
        ////////////////////
        //// Long stuff ////
        ////////////////////

        private async Task BlockGuildAsync(string serverId)
        {
            await DeferAsync();

            ulong guildId = ulong.Parse(serverId);
            var guild = await _db.Guilds.FindAsync(guildId);

            if (guild is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Server not found", Color.Red));
                return;
            }

            if ((await _db.BlockedGuilds.FindAsync(guild.Id)) is not null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Server is aready blocked", Color.Orange));
                return;
            }

            await _db.BlockedGuilds.AddAsync(new() { Id = guildId });
            _db.Guilds.Remove(guild); // Remove from db
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: InlineEmbed($"{OK_SIGN_DISCORD} Server was removed from the database", Color.Red));

            // Leave
            var discordGuild = _client.GetGuild(guildId);

            if (discordGuild is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Failed to leave the server", Color.Red));
                return;
            }

            await discordGuild.LeaveAsync();
            await FollowupAsync(embed: InlineEmbed($"{OK_SIGN_DISCORD} Server \"{discordGuild.Name}\" is leaved", Color.Red));
        }
    }
}
