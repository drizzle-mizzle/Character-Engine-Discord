using CharacterEngine.App.Handlers;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace CharacterEngine.Helpers.Discord;


public static class DiscordLaunchHelper
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();


    public static DiscordSocketClient CreateDiscordClient()
    {
        const GatewayIntents intents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.MessageContent | GatewayIntents.GuildWebhooks;

        var clientConfig = new DiscordSocketConfig
        {
            MessageCacheSize = 15,
            GatewayIntents = intents,
            ConnectionTimeout = 20_000,
            DefaultRetryMode = RetryMode.RetryRatelimit,
            AlwaysDownloadDefaultStickers = true,
        };

        return new DiscordSocketClient(clientConfig);
    }


    public static void BindEvents(this DiscordSocketClient discordClient, IServiceProvider sp)
    {
        discordClient.Log += msg =>
        {
            if (msg.Severity is LogSeverity.Error or LogSeverity.Critical)
            {
                _log.Error(msg.ToString());
            }
            else
            {
                _log.Info(msg.ToString());
            }

            return Task.CompletedTask;
        };

        discordClient.JoinedGuild += guild =>
        {
            _log.Info($"Joined guild {guild.Name} ({guild.Id}) | Members: {guild.MemberCount} | Description: {guild.Description ?? "none"}");
            return Task.CompletedTask;
        };

        discordClient.LeftGuild += guild =>
        {
            _log.Info($"Left guild {guild.Name} ({guild.Id}) | Members: {guild.MemberCount} | Description: {guild.Description ?? "none"}");
            return Task.CompletedTask;
        };

        var interactionService = sp.GetRequiredService<InteractionService>();

        interactionService.InteractionExecuted += sp.GetRequiredService<InteractionsHandler>().HandleInteractionAsync;
        discordClient.SlashCommandExecuted += sp.GetRequiredService<SlashCommandsHandler>().HandleSlashCommandAsync;
        discordClient.ModalSubmitted += sp.GetRequiredService<ModalsHandler>().HandleModalAsync;
        discordClient.ButtonExecuted += sp.GetRequiredService<ButtonsHandler>().HandleButtonAsync;

    }
}
