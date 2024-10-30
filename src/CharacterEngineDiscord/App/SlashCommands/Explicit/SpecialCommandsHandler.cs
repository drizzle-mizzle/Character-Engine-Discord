using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands.Explicit;


public class SpecialCommandsHandler
{
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public SpecialCommandsHandler(DiscordSocketClient discordClient, InteractionService interactions)
    {
        _discordClient = discordClient;
        _interactions = interactions;
    }


    public async Task HandleStartCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.GuildAdmin, (SocketGuildUser)command.User);

        var guild = _discordClient.Guilds.First(g => g.Id == command.GuildId);
        var disableCommand = ExplicitCommandBuilders.BuildDisableCommand();

        await guild.DeleteApplicationCommandsAsync();

        var createDisableCommand = guild.CreateApplicationCommandAsync(disableCommand);
        await _interactions.RegisterCommandsToGuildAsync(guild.Id, false);

        await createDisableCommand;

        await command.FollowupAsync("OK");
    }


    public async Task HandleDisableCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.GuildAdmin, (SocketGuildUser)command.User);

        var guild = _discordClient.Guilds.First(g => g.Id == command.GuildId);
        var startCommand = ExplicitCommandBuilders.BuildStartCommand();

        await guild.DeleteApplicationCommandsAsync();
        await guild.CreateApplicationCommandAsync(startCommand);

        await command.FollowupAsync("OK");
    }
}
