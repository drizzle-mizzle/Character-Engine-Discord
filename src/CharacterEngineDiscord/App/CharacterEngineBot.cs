using System.Collections.Concurrent;
using System.Reflection;
using CharacterEngine.Helpers;
using CharacterEngine.Helpers.Common;
using CharacterEngine.Helpers.Discord;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace CharacterEngine.App;


public static class CharacterEngineBot
{
    private static readonly IServiceProvider _services = CommonHelper.BuildServiceProvider();
    private static readonly DiscordSocketClient _discordClient = _services.GetRequiredService<DiscordSocketClient>();
    private static readonly InteractionService _interactions = _services.GetRequiredService<InteractionService>();
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();


    public static void Run()
        => RunAsync().GetAwaiter().GetResult();


    private static async Task RunAsync()
    {
        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services).ConfigureAwait(false);
        _discordClient.BindEvents(_services);
        _discordClient.Connected += OnConnected;

        await _discordClient.LoginAsync(TokenType.Bot, BotConfig.BOT_TOKEN).ConfigureAwait(false);
        await _discordClient.StartAsync().ConfigureAwait(false);

        BackgroundWorker.Run(_services);

        await Task.Delay(-1);
    }


    private static async Task OnConnected()
    {
        _log.Info($"{_discordClient.CurrentUser.Username} - Online");

        var guilds = await GetGuildsAsync();

        _log.Info($"Registering /start command to ({guilds.Count}) guilds...");

        await guilds.RegisterStartCommandAsync();

        _log.Info("Commands registered successfully");

        if (BotConfig.PLAYING_STATUS.Length != 0)
        {
            await _discordClient.SetGameAsync(BotConfig.PLAYING_STATUS);
            _log.Info($"Playing status - {BotConfig.PLAYING_STATUS}");
        }

        await _discordClient.ReportLogAsync("Online", null);
    }


    private static async Task<ConcurrentBag<SocketGuild>> GetGuildsAsync()
    {
        ConcurrentBag<SocketGuild> guilds = [];
        await Parallel.ForEachAsync(_discordClient.Guilds, async (guild, _) =>
        {
            try
            {
                var guildCommands = await guild.GetApplicationCommandsAsync();
                if (guildCommands.Count != 0)
                {
                    guilds.Add(guild);
                }
            }
            catch
            {
                // nvm
            }
        }).ConfigureAwait(false);

        return guilds;
    }


    private static async Task RegisterStartCommandAsync(this ConcurrentBag<SocketGuild> guilds)
    {
        await Parallel.ForEachAsync(guilds, async (guild, _) =>
        {
            try
            {
                var startCommand = DiscordInteractionsHelper.BuildStartCommand();
                await guild.CreateApplicationCommandAsync(startCommand).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _log.Error($"Failed to register command in guild {guild.Name} ({guild.Id}): {e}");
            }
        }).ConfigureAwait(false);
    }

}
