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
using Discord.Rest;

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
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            // Initialize handlers
            _services.GetRequiredService<ReactionsHandler>();
            _services.GetRequiredService<ButtonsHandler>();
            _services.GetRequiredService<SlashCommandsHandler>();
            _services.GetRequiredService<TextMessagesHandler>();
            _services.GetRequiredService<ModalsHandler>();

            await new StorageContext().Database.MigrateAsync();

            _client.Ready += CreateSlashCommandsAsync;
            _client.Log += (msg) => Task.Run(() => Log($"{msg}\n"));

            _client.JoinedGuild += OnGuildJoinAsync;
            _client.LeftGuild += (guild) => Task.Run(() => LogRed($"Left guild: {guild.Name} | Members: {guild?.MemberCount}\n"));

            await Task.Run(SetupIntegrationAsync);
            await _client.LoginAsync(TokenType.Bot, ConfigFile.DiscordBotToken.Value);
            await _client.StartAsync();
            
            await RunJobsAsync();
        }

        private async Task RunJobsAsync()
        {
            try
            {
                while (true)
                {
                    await TryToReportInLogsChannel(_client, "Uptime Status", desc: $"Running - {DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()}",
                                                                             content: null, color: Color.DarkGreen, error: false);
                    
                    var db = new StorageContext();
                    var blockedUsersToUnblock = db.BlockedUsers.Where(bu => bu.Hours != 0 && (bu.From.AddHours(bu.Hours) <= DateTime.UtcNow));
                    db.BlockedUsers.RemoveRange(blockedUsersToUnblock);
                    await db.SaveChangesAsync();

                    await Task.Delay(3_600_000); // 1 hour
                }
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                await TryToReportInLogsChannel(_client, "Exception", "Jobs", e.ToString(), Color.Red, true);
                _ = Task.Run(RunJobsAsync);
            }
        }

        private Task OnGuildJoinAsync(SocketGuild guild)
        {
            Task.Run(async () => { try
            {
                var db = new StorageContext();
                bool guildIsBlocked = (await db.BlockedGuilds.FindAsync(guild.Id)) is not null;
                if (guildIsBlocked)
                {
                    await guild.LeaveAsync();
                    return;
                }

                try { await _interactions.RegisterCommandsToGuildAsync(guild.Id); }
                catch (Exception e)
                {
                    LogException(new[] { e });
                    await TryToReportInLogsChannel(_client, $"{WARN_SIGN_DISCORD} Failed to register commands in guild", $"Guild: {guild.Name}\nOwner: {guild.Owner?.GetBestName()}", e.ToString(), Color.Red, error: true);
                }

                if (!(guild.Roles?.Any(r => r.Name == ConfigFile.DiscordBotRole.Value!) ?? false))
                    await guild.CreateRoleAsync(ConfigFile.DiscordBotRole.Value!, isMentionable: true);

                var guildOwner = await _client.GetUserAsync(guild.OwnerId);
                string log = $"Sever name: {guild.Name} ({guild.Id})\n" +
                                $"Owner: {guildOwner?.Username}{(guildOwner?.GlobalName is string gn ? $" ({gn})" : "")}\n" +
                                $"Members: {guild.MemberCount}\n" +
                                $"{(guild.Description is string desc ? $"Description: \"{desc}\"" : "")}";
                LogGreen(log);

                await TryToReportInLogsChannel(_client, "New server", log, null, Color.Green, false);
            } catch (Exception e) { LogException(new[] { e }); }});

            return Task.CompletedTask;
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

        private Task CreateSlashCommandsAsync()
        {
            Task.Run(async () =>
            {
                Log("Registering commands to guilds...\n");
                foreach (var guild in _client.Guilds)
                {
                    LogGreen(".");
                    try { await _interactions.RegisterCommandsToGuildAsync(guild.Id); }
                    catch (Exception e)
                    {
                        LogException(new[] { e });
                        await TryToReportInLogsChannel(_client, $"{WARN_SIGN_DISCORD} Exception", $"Failed to register commands in guild:\n{e}", null, Color.Green, error: false);
                        continue;
                    }
                }
                Log("\n");
                await TryToReportInLogsChannel(_client, "Notification", "Commands registered successfuly\n", null, Color.Green, error: false);
                LogGreen("Commands registered successfuly\n");
            });

            return Task.CompletedTask;
        }

        private static DiscordSocketClient CreateDiscordClient()
        {
            // Define GatewayIntents
            var intents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.MessageContent | GatewayIntents.GuildWebhooks;

            // Create client
            var clientConfig = new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                GatewayIntents = intents,
                ConnectionTimeout = 30000,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                AlwaysDownloadDefaultStickers = true,
            };

            return new DiscordSocketClient(clientConfig);
        }
    }
}
