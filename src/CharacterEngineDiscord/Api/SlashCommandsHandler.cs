using CharacterEngine.Api.Abstractions;
using CharacterEngine.Helpers;
using CharacterEngine.Helpers.Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.Api;


public class SlashCommandsHandler : HandlerBase
{
    public static async Task HandleSlashCommandAsync(SocketSlashCommand command)
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
                    await Interactions.ExecuteCommandAsync(context, Services);

                    return;
                }
            }
        }
        catch (Exception e)
        {
            await DiscordClient.ReportErrorAsync(e);
        }
    }


    private static async Task HandleStartCommandAsync(SocketSlashCommand command)
    {
        var guild = DiscordClient.Guilds.First(g => g.Id == command.GuildId);
        var disableCommand = DiscordCommandsHelper.BuildDisableCommand();

        await guild.DeleteApplicationCommandsAsync();
        await guild.CreateApplicationCommandAsync(disableCommand);
        await Interactions.RegisterCommandsToGuildAsync(guild.Id, false);

        await command.FollowupAsync("OK");
    }


    private static async Task HandleDisableCommandAsync(SocketSlashCommand command)
    {
        var guild = DiscordClient.Guilds.First(g => g.Id == command.GuildId);
        var startCommand = DiscordCommandsHelper.BuildStartCommand();

        await guild.DeleteApplicationCommandsAsync();
        await guild.CreateApplicationCommandAsync(startCommand);

        await command.FollowupAsync("OK");
    }
}
