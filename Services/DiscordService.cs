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
        private bool _firstLaunch = true;

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

            _client.Log += (msg) => Task.Run(() => Log($"{msg}\n"));
            _client.LeftGuild += (guild) => Task.Run(() => LogRed($"Left guild: {guild.Name} | Members: {guild?.MemberCount}\n"));
            _client.JoinedGuild += (guild) =>
            {
                Task.Run(async () => await OnGuildJoinAsync(guild));
                return Task.CompletedTask;
            };

            _client.Ready += () =>
            {
                Task.Run(async () => await OnClientReadyAsync());
                return Task.CompletedTask;
            };

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
                    var db = new StorageContext();

                    var time = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                    int blockedUsersCount = db.BlockedUsers.Where(bu => bu.GuildId == null).Count();
                    string text = $"Running: `{time.Days}d/{time.Hours}h`\n" +
                                  $"Blocked: `{blockedUsersCount} user(s)` | `{db.BlockedGuilds.Count()} guild(s)`";

                    
                    var blockedUsersToUnblock = db.BlockedUsers.Where(bu => bu.Hours != 0 && (bu.From.AddHours(bu.Hours) <= DateTime.UtcNow));
                    db.BlockedUsers.RemoveRange(blockedUsersToUnblock);
                    await db.SaveChangesAsync();

                    _integration.WatchDogClear();
                    _integration.WebhookClients.Clear();

                    if (_integration.CaiClient is not null)
                    {
                        _integration.CaiClient.KillBrowser();
                        await _integration.CaiClient.LaunchBrowserAsync(killDuplicates: true);
                    }

                    await TryToReportInLogsChannel(_client, "Status", desc: text, content: null, color: Color.DarkGreen, error: false);
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

        private async Task OnClientReadyAsync()
        {
            if (!_firstLaunch) return;

            Log("Registering commands to guilds...\n");
            await Parallel.ForEachAsync(_client.Guilds, async (guild, ct) =>
            {
                if (await TryToCreateSlashCommandsAndRoleAsync(guild, silent: true)) LogGreen(".");
                else LogRed(".");
            });

            LogGreen("\nCommands registered successfuly\n");
            await TryToReportInLogsChannel(_client, "Notification", "Commands registered successfuly\n", null, Color.Green, error: false);

            _firstLaunch = false;
        }

        private async Task OnGuildJoinAsync(SocketGuild guild)
        {
            try
            {
                if ((await new StorageContext().BlockedGuilds.FindAsync(guild.Id)) is not null)
                {
                    await guild.LeaveAsync();
                    return;
                };

                await TryToCreateSlashCommandsAndRoleAsync(guild, silent: false);

                var guildOwner = await _client.GetUserAsync(guild.OwnerId);
                string log = $"Sever name: {guild.Name} ({guild.Id})\n" +
                                $"Owner: {guildOwner?.Username}{(guildOwner?.GlobalName is string gn ? $" ({gn})" : "")}\n" +
                                $"Members: {guild.MemberCount}\n" +
                                $"{(guild.Description is string desc ? $"Description: \"{desc}\"" : "")}";
                LogGreen(log);

                await TryToReportInLogsChannel(_client, "New server", log, null, Color.Green, false);
            }
            catch { return; }
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

        private async Task<bool> TryToCreateSlashCommandsAndRoleAsync(SocketGuild guild, bool silent)
        {                
            try
            {
                await _interactions.RegisterCommandsToGuildAsync(guild.Id);
                if (!(guild.Roles?.Any(r => r.Name == ConfigFile.DiscordBotRole.Value!) ?? false))
                    await guild.CreateRoleAsync(ConfigFile.DiscordBotRole.Value!, isMentionable: true);

                return true;
            }
            catch (Exception e)
            {
                if (!silent)
                {
                    LogException(new[] { e });
                    await TryToReportInLogsChannel(_client, $"{WARN_SIGN_DISCORD} Exception", $"Failed to register commands in guild:\n{e}", null, Color.Green, error: false);
                }

                return false;
            }
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
