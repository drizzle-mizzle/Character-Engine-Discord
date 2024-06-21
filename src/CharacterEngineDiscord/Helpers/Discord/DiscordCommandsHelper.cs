using Discord;

namespace CharacterEngine.Helpers.Discord;


public class DiscordCommandsHelper
{
    public static SlashCommandProperties BuildStartCommand()
        => new SlashCommandBuilder().WithName("start").WithDescription("Register bot commands").WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild).Build();


    public static SlashCommandProperties BuildDisableCommand()
        => new SlashCommandBuilder().WithName("disable").WithDescription("Unregister all bot commands").WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild).Build();
}
