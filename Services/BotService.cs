﻿using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Handlers;
using CharacterEngineDiscord.Models.Common;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.DatabaseContext;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using CharacterEngineDiscord.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Services
{
    internal class BotService
    {
        private ServiceProvider _services = null!;
        private DiscordSocketClient _client = null!;
        private IIntegrationsService _integrations = null!;
        private InteractionService _interactionService = null!;

        private bool _firstLaunch = true;

        internal async Task LaunchAsync(bool noreg)
        {
            await using (var db = new DatabaseContext())
                await db.Database.MigrateAsync();

            CreateServices();
            _client = (_services.GetRequiredService<IDiscordClient>() as DiscordSocketClient)!;

            SetupIntegration();
            SetupInteractions();            
            BindClientEvents(noreg);
            
            await _client.LoginAsync(TokenType.Bot, ConfigFile.DiscordBotToken.Value);
            await _client.StartAsync();

            await RunJobsAsync();
        }

        private void BindClientEvents(bool noreg)
        {
            _client.Log += (msg) =>
            {
                Log($"{msg}\n");
                return Task.CompletedTask;
            };

            _client.Connected += () =>
            {
                Task.Run(async () => await OnConnectedAsync(noreg));
                return Task.CompletedTask;
            };

            _client.JoinedGuild += (guild) =>
            {
                Task.Run(async () => await OnGuildJoinAsync(guild));
                return Task.CompletedTask;
            };

            _client.LeftGuild += OnGuildLeft;

            _client.ButtonExecuted += _services.GetRequiredService<ButtonsHandler>().HandleButton;
            _client.ModalSubmitted += _services.GetRequiredService<ModalsHandler>().HandleModal;
            _client.ReactionAdded += _services.GetRequiredService<ReactionsHandler>().HandleReaction;
            _client.ReactionRemoved += _services.GetRequiredService<ReactionsHandler>().HandleReaction;
            _client.SlashCommandExecuted += _services.GetRequiredService<SlashCommandsHandler>().HandleCommand;
            _client.MessageReceived += _services.GetRequiredService<TextMessagesHandler>().HandleMessage;
            _interactionService.InteractionExecuted += (_, context, result)
                => result.IsSuccess ? Task.CompletedTask : HandleInteractionException(context, result);
        }


        private void DoJobs()
        {
            var time = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

            int blockedUsersCount;
            int blockedGuildsCount;
            using (var db = new DatabaseContext())
            {
                blockedUsersCount = db.BlockedUsers.Count(bu => bu.GuildId == null);
                blockedGuildsCount = db.BlockedGuilds.Count();
            }

            string text = $"Running: `{time.Days}d/{time.Hours}h/{time.Minutes}m`\n" +
                          $"Messages sent: `{_integrations.MessagesSent}`\n" +
                          $"Blocked: `{blockedUsersCount} user(s)` | `{blockedGuildsCount} guild(s)`";

            TryToReportInLogsChannel(_client, "Status", desc: text, content: null, color: Color.DarkGreen, error: false);
            UnblockUsers();
            ClearTempResources();
        }

        private static void UnblockUsers()
        {
            using var db = new DatabaseContext();

            var blockedUsersToUnblock = db.BlockedUsers.Where(user => user.Hours != 0 && (user.From.AddHours(user.Hours) <= DateTime.UtcNow));
            db.BlockedUsers.RemoveRange(blockedUsersToUnblock);
            TryToSaveDbChangesAsync(db).Wait();
        }

        private void ClearTempResources()
        {
            if (_firstLaunch) return;

            _integrations.WatchDogClear();
            _integrations.WebhookClients.Clear();

            var oldConvosToStopTrack = _integrations.Conversations.Where(c => (DateTime.UtcNow - c.Value.LastUpdated).Hours > 5);
            foreach (var convo in oldConvosToStopTrack)
                _integrations.Conversations.Remove(convo.Key);

            if (_integrations.CaiClient is null) return;

            for (int i = 0; i < 10; i++)
            {
                if (_integrations.RunningCaiTasks.Count == 0)
                    break;
                Task.Delay(1000);
            }

            _integrations.CaiReloading = true;
            _integrations.RunningCaiTasks.Clear();
            _integrations.CaiClient.ReconnectAsync().Wait();
            _integrations.CaiReloading = false;
        }

        private async Task OnConnectedAsync(bool noreg)
        {
            if (!_firstLaunch) return;

            LogYellow($"[ {_client.CurrentUser.Username} ]\n\n");
            if (!noreg)
            {
                Log($"Registering commands to ({_client.Guilds.Count}) guilds...\n");
                await Parallel.ForEachAsync(_client.Guilds, async (guild, _) =>
                {
                    if (await TryToCreateSlashCommandsAndRoleAsync(guild)) LogGreen(".");
                    else LogRed(".");
                });

                LogGreen("\nCommands registered successfully\n");
                TryToReportInLogsChannel(_client, "Notification", "Commands registered successfully\n", null,
                    Color.Green, error: false);
            }

            await _client.SetGameAsync(GetLastGameStatus());

            _firstLaunch = false;
        }

        private async Task OnGuildJoinAsync(SocketGuild guild)
        {
            try
            {
                await using (var db = new DatabaseContext())
                {
                    if (db.BlockedGuilds.Any(bg => bg.Id.Equals(guild.Id)))
                    {
                        await guild.LeaveAsync();
                        return;
                    };
                }

                await TryToCreateSlashCommandsAndRoleAsync(guild);

                var guildOwner = await _client.GetUserAsync(guild.OwnerId);
                string log = $"Server name: {guild.Name} ({guild.Id})\n" +
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
            string log = $"Server name: {guild.Name} ({guild.Id})\n" +
                         $"Members: {guild.MemberCount}\n" +
                         $"{(guild.Description is string desc ? $"Description: \"{desc}\"" : "")}";

            LogRed("\nLeft server\n" + log);
            TryToReportInLogsChannel(_client, "Left server", log, null, Color.Orange, false);

            return Task.CompletedTask;
        }

        private void CreateServices()
        {
            var discordClient = CreateDiscordClient();
            var interactionService = new InteractionService(discordClient.Rest);

            var services = new ServiceCollection()
                // Singletones
                .AddSingleton(interactionService)
                .AddSingleton<IIntegrationsService, IntegrationsService>()
                .AddSingleton<IDiscordClient>(discordClient)
                // Handlers
                .AddTransient<ButtonsHandler>()
                .AddTransient<ModalsHandler>()
                .AddTransient<ReactionsHandler>()
                .AddTransient<SlashCommandsHandler>()
                .AddTransient<TextMessagesHandler>();

            _services = services.BuildServiceProvider();
        }

        private async Task<bool> TryToCreateSlashCommandsAndRoleAsync(SocketGuild guild)
        {                
            try
            {
                await _interactionService.RegisterCommandsToGuildAsync(guild.Id);
                if (!(guild.Roles?.Any(r => r.Name == ConfigFile.DiscordBotRole.Value!) ?? false))
                    await guild.CreateRoleAsync(ConfigFile.DiscordBotRole.Value!, isMentionable: true);

                return true;
            }
            catch (Exception e)
            {
                LogException(e);
                return false;
            }
        }

        private static DiscordSocketClient CreateDiscordClient()
        {
            // Define GatewayIntents
            const GatewayIntents intents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.MessageContent | GatewayIntents.GuildWebhooks;

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

        private void SetupIntegration()
        {
            try
            {
                _integrations = _services.GetRequiredService<IIntegrationsService>();
                _integrations.Initialize();
            }
            catch (Exception e) { LogException(e); }
        }

        private void SetupInteractions()
        {
            try
            {
                _interactionService = _services.GetRequiredService<InteractionService>();
                _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _services).Wait();
            }
            catch (Exception e) { LogException(e); }
        }


        private static string GetLastGameStatus()
        {
            string gamePath = $"{EXE_DIR}{SC}storage{SC}lastgame.txt";
            return File.Exists(gamePath) ? File.ReadAllText(gamePath) : string.Empty;
        }


        private static Task HandleInteractionException(IInteractionContext context, IResult result)
        {
            Task.Run(async () =>
            {
                string er = result.ErrorReason;
                if (!er.Contains("Microsoft") && !er.Contains("This command"))
                    try { await context.Interaction.RespondAsync(embed: $"{WARN_SIGN_DISCORD} Failed to execute command: `{er}`".ToInlineEmbed(Color.Red)); }
                    catch { await context.Interaction.FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to execute command: `{er}`".ToInlineEmbed(Color.Red)); }

                var channel = context.Channel;
                var guild = context.Guild;

                // don't need that shit in logs
                bool ignore = er.Contains("UnmetPrecondition") || er.Contains("This command") || er.Contains("was not in a correct format");
                if (ignore) return;

                var originalResponse = await context.Interaction.GetOriginalResponseAsync();
                var owner = (await guild.GetOwnerAsync()) as SocketGuildUser;

                TryToReportInLogsChannel(context.Client, title: "Slash Command Exception",
                                                            desc: $"Guild: `{guild.Name} ({guild.Id})`\n" +
                                                                $"Owner: `{owner?.GetBestName()} ({owner?.Username})`\n" +
                                                                $"Channel: `{channel.Name} ({channel.Id})`\n" +
                                                                $"User: `{context.User.Username}`\n" +
                                                                $"Slash command: `/{originalResponse.Interaction.Name}`",
                                                            content: $"{er}\n\n{(result as ExecuteResult?)?.Exception}",
                                                            color: Color.Red,
                                                            error: true);
            });

            return Task.CompletedTask;
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
                    LogException(e);
                    TryToReportInLogsChannel(_client, "Exception", "Jobs", e.ToString(), Color.Red, true);
                }
                finally
                {
                    await Task.Delay(1_800_000); // 30 min
                }
            }
        }
    }
}
