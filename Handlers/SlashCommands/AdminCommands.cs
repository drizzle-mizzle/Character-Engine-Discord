using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [RequireAdminAccess]
    [Group("admin", "Admin commands")]
    public class AdminCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly StorageContext _db;

        public AdminCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _db = services.GetRequiredService<StorageContext>();
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
                var sgc = (await Context.Client.GetChannelAsync(channelId)) as IMessageChannel;
                if (sgc is not null) channels.Add(sgc);
            }

            foreach (var channel in channels)
                await channel.SendMessageAsync(embed: embed.Build());

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        [SlashCommand("shutdown", "Shutdown")]
        public async Task ShutdownAsync()
        {
            var user = Context.User as SocketGuildUser;
            if (user.IsHoster())
            {
                await RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Shutting down...", Color.Orange));
                try { _integration?.CaiClient?.KillBrowser(); }
                catch (Exception e) { LogException(new[] { "Failed to kill Puppeteer processes.\n", e.ToString() }); }
                Environment.Exit(0);
            }
            else
                await Context.SendNoPowerFileAsync();
        }


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
            var discordGuild = await Context.Client.GetGuildAsync(guildId);

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
