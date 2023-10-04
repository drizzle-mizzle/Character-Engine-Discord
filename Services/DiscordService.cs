using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Handlers;
using CharacterEngineDiscord.Models.Common;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

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

            BindEvents();
            SetupIntegrationAsync();

            await _client.LoginAsync(TokenType.Bot, ConfigFile.DiscordBotToken.Value);
            await _client.StartAsync();

            await RunJobsAsync();
        }

        private void BindEvents()
        {
            _client.Log += (msg) =>
            {
                Log($"{msg}\n");
                return Task.CompletedTask;
            };

            _client.Ready += () =>
            {
                Task.Run(async () => await OnClientReadyAsync());
                return Task.CompletedTask;
            };

            _client.JoinedGuild += (guild) =>
            {
                Task.Run(async () => await OnGuildJoinAsync(guild));
                return Task.CompletedTask;
            };

            _client.LeftGuild += OnGuildLeft;
        }

        private async Task RunJobsAsync()
        {
            while (true)
            {
                try
                {
                    DoJobs();
                }
                catch (Exception e)
                {
                    LogException(new[] { e });
                    TryToReportInLogsChannel(_client, "Exception", "Jobs", e.ToString(), Color.Red, true);
                }
                finally
                {
                    await Task.Delay(1_800_000); // 30 min
                }
            }
        }

        private void DoJobs()
        {
            var db = new StorageContext();

            var time = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            int blockedUsersCount = db.BlockedUsers.Where(bu => bu.GuildId == null).Count();

            string text = $"Running: `{time.Days}d/{time.Hours}h/{time.Minutes}m`\n" +
                          $"Messages sent: `{_integration.MessagesSent}`\n" +
                          $"Blocked: `{blockedUsersCount} user(s)` | `{db.BlockedGuilds.Count()} guild(s)`";

            TryToReportInLogsChannel(_client, "Status", desc: text, content: null, color: Color.DarkGreen, error: false);
            UnblockUsers();
            ClearTempsAndRelaunchCai();
        }

        private static void UnblockUsers()
        {
            var db = new StorageContext();

            var blockedUsersToUnblock = db.BlockedUsers.Where(user => user.Hours != 0 && (user.From.AddHours(user.Hours) <= DateTime.UtcNow));
            db.BlockedUsers.RemoveRange(blockedUsersToUnblock);

            db.SaveChanges();
        }

        private void ClearTempsAndRelaunchCai()
        {
            if (_firstLaunch) return;

            _integration.WatchDogClear();
            _integration.WebhookClients.Clear();

            var oldConvosToStopTrack = _integration.Conversations.Where(c => (DateTime.UtcNow - c.Value.LastUpdated).Hours > 5);
            foreach (var convo in oldConvosToStopTrack)
                _integration.Conversations.Remove(convo.Key);

            if (_integration.CaiClient is not null)
            {
                _integration.CaiClient.KillBrowser();
                _integration.CaiClient.LaunchBrowser(killDuplicates: true);
            }
        }

        private async Task OnClientReadyAsync()
        {
            if (!_firstLaunch) return;

            Log($"Registering commands to ({_client.Guilds.Count}) guilds...\n");
            await Parallel.ForEachAsync(_client.Guilds, async (guild, ct) =>
            {
                if (await TryToCreateSlashCommandsAndRoleAsync(guild, silent: true)) LogGreen(".");
                else LogRed(".");
            });

            LogGreen("\nCommands registered successfully\n");
            TryToReportInLogsChannel(_client, "Notification", "Commands registered successfully\n", null, Color.Green, error: false);
            
            await _client.SetGameAsync(GetLastGameStatus());

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
                LogGreen("\nJoined server\n" + log);
                TryToReportInLogsChannel(_client, "New server", log, null, Color.Gold, false);
            }
            catch { return; }
        }

        private Task OnGuildLeft(SocketGuild guild)
        {
            string log = $"Sever name: {guild.Name} ({guild.Id})\n" +
                         $"Owner: {guild.Owner.Username}\n" +
                         $"Members: {guild.MemberCount}\n" +
                         $"{(guild.Description is string desc ? $"Description: \"{desc}\"" : "")}";

            LogRed("\nLeft server\n" + log);
            TryToReportInLogsChannel(_client, "Left server", log, null, Color.Orange, false);

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

        private void SetupIntegrationAsync()
        {
            try { _integration.Initialize(); }
            catch (Exception e) { LogException(new[] { e }); }
        }

        private static string GetLastGameStatus()
        {
            string gamePath = $"{EXE_DIR}{SC}storage{SC}lastgame.txt";
            return File.Exists(gamePath) ? File.ReadAllText(gamePath) : string.Empty;
        }
    }
}
