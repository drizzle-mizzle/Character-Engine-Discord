using CharacterEngine.Helpers.Discord;
using CharacterEngineDiscord.Db;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.Handlers;


public class SlashCommandsHandler
{
    public required LocalStorage LocalStorage { get; set; }
    public required DiscordSocketClient DiscordClient { get; set; }
    public required AppDbContext db { get; set; }
    public required InteractionService Interactions { get; set; }
    public required IServiceProvider ServiceProvider { get; set; }


    public async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        try
        {
            switch (command.CommandName)
            {
                case "start":
                {
                    await command.DeferAsync();
                    await HandleStartCommandAsync(command);
                    return;
                }
                case "disable":
                {
                    await command.DeferAsync();
                    await HandleDisableCommandAsync(command);
                    return;
                }
                default:
                {
                    var context = new InteractionContext(DiscordClient, command, command.Channel);
                    await Interactions.ExecuteCommandAsync(context, ServiceProvider);

                    return;
                }
            }
        }
        catch (Exception e)
        {
            await DiscordClient.ReportErrorAsync(e);
        }
    }


    private async Task HandleStartCommandAsync(SocketSlashCommand command)
    {
        var guild = DiscordClient.Guilds.First(g => g.Id == command.GuildId);
        var disableCommand = DiscordInteractionsHelper.BuildDisableCommand();

        await guild.DeleteApplicationCommandsAsync();
        await guild.CreateApplicationCommandAsync(disableCommand);
        await Interactions.RegisterCommandsToGuildAsync(guild.Id, false);

        await command.FollowupAsync("OK");
    }


    private async Task HandleDisableCommandAsync(SocketSlashCommand command)
    {
        var guild = DiscordClient.Guilds.First(g => g.Id == command.GuildId);
        var startCommand = DiscordInteractionsHelper.BuildStartCommand();

        await guild.DeleteApplicationCommandsAsync();
        await guild.CreateApplicationCommandAsync(startCommand);

        await command.FollowupAsync("OK");
    }
    
}
