using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    public class GlobalCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly StorageContext _db;
        public GlobalCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("shutdown", "Shutdown")]
        public async Task ShutdownAsync()
        {
            await RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Shutting down...", Color.Orange));
            try { _integration?.CaiClient?.KillBrowser(); }
            catch (Exception e) { LogException(new[] { "Failed to kill Puppeteer processes.\n", e.ToString() }); }
            Environment.Exit(0);
        }

        [SlashCommand("shout-out", "Send a message in each channel where bot was ever called")]
        public async Task ShoutOut(string? title, string? desc, string? imageUrl)
        {
            await ShoutOutAsync(title, desc, imageUrl);
        }

        private async Task ShoutOutAsync(string? title, string? desc, string? imageUrl)
        {
            await DeferAsync();

            var embed = new EmbedBuilder();
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
    }
}
