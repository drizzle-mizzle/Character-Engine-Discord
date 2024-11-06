using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Helpers.Discord;
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
        await command.RespondAsync(embed: MessagesTemplates.WAIT_MESSAGE);

        await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.GuildAdmin, (SocketGuildUser)command.User);

        var guild = _discordClient.Guilds.First(g => g.Id == command.GuildId);
        var disableCommand = ExplicitCommandBuilders.BuildDisableCommand();

        var registeredCommands = await guild.GetApplicationCommandsAsync();
        foreach (var registeredCommand in registeredCommands)
        {
            await registeredCommand.DeleteAsync();
        }

        await _interactions.RegisterCommandsToGuildAsync(guild.Id);
        await guild.CreateApplicationCommandAsync(disableCommand);

        const string message = "**Thank you for using Character Engine!**\nUse *`/integration create`* command to begin.";

        await command.FollowupAsync(embed: message.ToInlineEmbed(Color.Gold, bold: false));
    }


    public async Task HandleDisableCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.GuildAdmin, (SocketGuildUser)command.User);

        var guild = _discordClient.Guilds.First(g => g.Id == command.GuildId);
        var startCommand = ExplicitCommandBuilders.BuildStartCommand();

        await guild.DeleteApplicationCommandsAsync();
        await guild.CreateApplicationCommandAsync(startCommand);

        await command.FollowupAsync("bye D:");
    }
}
