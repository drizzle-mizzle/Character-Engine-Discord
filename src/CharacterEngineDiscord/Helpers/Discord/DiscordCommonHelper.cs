using CharacterEngine.Api;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using NLog;

namespace CharacterEngine.Helpers.Discord;


public static class DiscordCommonHelper
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();


    public static DiscordSocketClient CreateDiscordClient()
    {
        // Define GatewayIntents
        const GatewayIntents intents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.MessageContent | GatewayIntents.GuildWebhooks;

        // Create client
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


    public static void BindEvents(this DiscordSocketClient discordClient, IServiceProvider serviceProvider)
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


        discordClient.SlashCommandExecuted += SlashCommandsHandler.HandleSlashCommandAsync;
        discordClient.ModalSubmitted += ModalsHandler.HandleModalAsync;
        discordClient.ButtonExecuted += ButtonsHandler.HandleButtonAsync;
    }
}
