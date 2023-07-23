using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Handlers;
using CharacterEngineDiscord.Models.Common;
using static CharacterEngineDiscord.Services.CommonService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

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
            string? envToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            Environment.SetEnvironmentVariable("DISCORD_TOKEN", null);

            _services = CreateServices();
            _services.GetRequiredService<TextMessagesHandler>();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _integration = _services.GetRequiredService<IntegrationsService>();
            _interactions = _services.GetRequiredService<InteractionService>();
            _services.GetRequiredService<ButtonsAndReactionsHandler>();
            _services.GetRequiredService<TextMessagesHandler>();
            _services.GetRequiredService<SlashCommandsHandler>();


            await _services.GetRequiredService<StorageContext>().Database.MigrateAsync();

            _client.Ready += () =>
            {
                Task.Run(async () => await CreateSlashCommandsAsync());
                Task.Run(async () => await SetupIntegrationAsync());
                return Task.CompletedTask;
            };

            await _client.LoginAsync(TokenType.Bot, envToken ?? ConfigFile.DiscordBotToken.Value);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        internal static ServiceProvider CreateServices()
        {
            var discordClient = CreateDiscordClient();

            var services = new ServiceCollection()
                .AddSingleton(discordClient)
                .AddSingleton<SlashCommandsHandler>()
                .AddSingleton<TextMessagesHandler>()
                .AddSingleton<ButtonsAndReactionsHandler>()
                .AddSingleton<IntegrationsService>()
                .AddScoped<StorageContext>()
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
            try
            {
                await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

                foreach (var guild in _client.Guilds)
                    await _interactions.RegisterCommandsToGuildAsync(guild.Id);
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
            client.JoinedGuild += (guild) => Task.Run(()
                => LogYellow($"Joined guild: {guild.Name} | Owner: {guild.Owner.Username} | Members: {guild.MemberCount}"));

            // So it would simply not show that annoying error in console...
            // Presence Intent itself is actually needed for some commands which require a user as an argument
            client.PresenceUpdated += (a, b, c) => Task.CompletedTask;

            return client;
        }
    }
}
