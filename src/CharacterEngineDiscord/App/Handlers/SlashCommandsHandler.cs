using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
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
            await (command.CommandName switch
            {
                "start" => HandleStartCommandAsync(command),
                "disable" => HandleDisableCommandAsync(command),
                _ => Interactions.ExecuteCommandAsync(new InteractionContext(DiscordClient, command, command.Channel), ServiceProvider)
            });
        }
        catch (Exception e)
        {
            await DiscordClient.ReportErrorAsync(e);
        }
    }


    private async Task HandleStartCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var guild = DiscordClient.Guilds.First(g => g.Id == command.GuildId);
        var disableCommand = InteractionsHelper.BuildDisableCommand();

        await guild.DeleteApplicationCommandsAsync();
        await guild.CreateApplicationCommandAsync(disableCommand);
        await Interactions.RegisterCommandsToGuildAsync(guild.Id, false);

        await command.FollowupAsync("OK");
    }


    private async Task HandleDisableCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var guild = DiscordClient.Guilds.First(g => g.Id == command.GuildId);
        var startCommand = InteractionsHelper.BuildStartCommand();

        await guild.DeleteApplicationCommandsAsync();
        await guild.CreateApplicationCommandAsync(startCommand);

        await command.FollowupAsync("OK");
    }
    
}
