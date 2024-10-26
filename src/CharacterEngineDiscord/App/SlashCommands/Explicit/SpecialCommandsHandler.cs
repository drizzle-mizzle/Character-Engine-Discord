using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands.Explicit;


public class SpecialCommandsHandler
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public SpecialCommandsHandler(AppDbContext db, DiscordSocketClient discordClient, InteractionService interactions)
    {
        _db = db;
        _discordClient = discordClient;
        _interactions = interactions;
    }


    public async Task HandleStartCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var guild = _discordClient.Guilds.First(g => g.Id == command.GuildId);
        var disableCommand = ExplicitCommandBuilders.BuildDisableCommand();

        var commands = await guild.GetApplicationCommandsAsync();

        foreach (var installedCommand in commands)
        {
            await installedCommand.DeleteAsync();
        }

        await guild.CreateApplicationCommandAsync(disableCommand);
        await _interactions.RegisterCommandsToGuildAsync(guild.Id, false);

        await command.FollowupAsync("OK");
    }


    public async Task HandleDisableCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var guild = _discordClient.Guilds.First(g => g.Id == command.GuildId);
        var startCommand = ExplicitCommandBuilders.BuildStartCommand();

        await guild.DeleteApplicationCommandsAsync();
        await guild.CreateApplicationCommandAsync(startCommand);

        await command.FollowupAsync("OK");
    }
}
