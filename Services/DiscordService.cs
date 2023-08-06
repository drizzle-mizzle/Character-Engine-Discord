using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Handlers;
using CharacterEngineDiscord.Models.Common;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace CharacterEngineDiscord.Services
{
    internal class DiscordService
    {
        private ServiceProvider _services = null!;
        private DiscordSocketClient _client = null!;
        private IntegrationsService _integration = null!;
        private InteractionService _interactions = null!;

        internal async Task SetupDiscordClient()
        {
            _services = CreateServices();

            _client = _services.GetRequiredService<DiscordSocketClient>();
            _integration = _services.GetRequiredService<IntegrationsService>();
            _interactions = _services.GetRequiredService<InteractionService>();

            // Initialize handlers
            _services.GetRequiredService<ReactionsHandler>();
            _services.GetRequiredService<ButtonsHandler>();
            _services.GetRequiredService<SlashCommandsHandler>();
            _services.GetRequiredService<TextMessagesHandler>();
            _services.GetRequiredService<ModalsHandler>();

            await new StorageContext().Database.MigrateAsync();

            _client.Ready += () =>
            {
                Task.Run(async () => await CreateSlashCommandsAsync());
                Task.Run(async () => await SetupIntegrationAsync());
                Task.Run(async () => await RunJobs());

                return Task.CompletedTask;
            };

            _client.JoinedGuild += (guild) =>
            {
                Task.Run(async () => await OnGuildJoinAsync(guild));
                return Task.CompletedTask;
            };

            _client.LeftGuild += (guild) =>
            {
                LogRed($"Left guild: {guild.Name} | Members: {guild?.MemberCount}\n");
                return Task.CompletedTask;
            };

            await _client.LoginAsync(TokenType.Bot, ConfigFile.DiscordBotToken.Value);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task RunJobs()
        {
            await TryToReportInLogsChannel(_client, "Uptime Status", desc: $"Running - {DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()}", color: Color.DarkGreen);

            var db = new StorageContext();
            var blockedUsersToUnblock = db.BlockedUsers.Where(bu => bu.Hours != 0 && (bu.From.AddHours(bu.Hours) < DateTime.UtcNow));
            db.BlockedUsers.RemoveRange(blockedUsersToUnblock);

            await Task.Delay(3_600_000); // 1 hour
        }

        private async Task OnGuildJoinAsync(SocketGuild guild)
        {
            try
            {
                var db = new StorageContext();
                bool guildIsBlocked = (await db.BlockedGuilds.FindAsync(guild.Id)) is not null;
                if (guildIsBlocked)
                {
                    await guild.LeaveAsync();
                    return;
                }

                await _interactions.RegisterCommandsToGuildAsync(guild.Id);

                if (!(guild.Roles?.Any(r => r.Name == ConfigFile.DiscordBotRole.Value!) ?? false))
                {
                    var role = await guild.CreateRoleAsync(ConfigFile.DiscordBotRole.Value!, isMentionable: true);
                }

                var guildOwner = await _client.GetUserAsync(guild.OwnerId);

                string log = $"Sever name: {guild.Name}\n" +
                             $"Owner: {guildOwner?.Username}{(guildOwner?.GlobalName is string gn ? $" ({gn})" : "")}\n" +
                             $"Members: {guild.MemberCount}\n" +
                             $"{(guild.Description is string desc ? $"Description: \"{desc}\"" : "")}";
                LogGreen(log);

                await TryToReportInLogsChannel(_client, title: "New server", desc: log, color: Color.Green);
            }
            catch (Exception e)
            {
                LogException(new[] { e });
            }
        }

        internal static ServiceProvider CreateServices()
        {
            var discordClient = CreateDiscordClient();
            var services = new ServiceCollection()
            .AddSingleton(discordClient)
            .AddSingleton<SlashCommandsHandler>()
            .AddSingleton<TextMessagesHandler>()
            .AddSingleton<ReactionsHandler>()
            .AddSingleton<ButtonsHandler>()
            .AddSingleton<ModalsHandler>()
            .AddSingleton<IntegrationsService>()
            .AddSingleton(new InteractionService(discordClient.Rest));

            return services.BuildServiceProvider();
        }

        private async Task SetupIntegrationAsync()
        {
            try { await _integration.Initialize(); }
            catch (Exception e) { LogException(new[] { e }); }
        }

        private async Task CreateSlashCommandsAsync()
        {
            try {
                await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
                foreach (var guild in _client.Guilds)
                    await _interactions.RegisterCommandsToGuildAsync(guild.Id);

                await TryToReportInLogsChannel(_client, "Notification", "Commands registered successfuly\n", Color.Green);
            }
            catch (Exception e) { LogException(new[] { e }); }
        }

        private static DiscordSocketClient CreateDiscordClient()
        {
            // Define GatewayIntents
            var intents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.MessageContent | GatewayIntents.GuildWebhooks;

            // Create client
            var clientConfig = new DiscordSocketConfig { MessageCacheSize = 5, GatewayIntents = intents };
            var client = new DiscordSocketClient(clientConfig);

            // Bind event handlers
            client.Log += (msg) => Task.Run(() => Log($"{msg}\n"));

            return client;
        }
    }
}
