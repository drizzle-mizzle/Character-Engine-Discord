using Discord;

namespace CharacterEngine.App.Helpers.Discord;


public enum ExplicitCommands
{
    start, disable
}


public static class ExplicitCommandBuilders
{
    public static SlashCommandProperties BuildStartCommand()
        => new SlashCommandBuilder().WithName(ExplicitCommands.start.ToString("G"))
                                    .WithDescription("Register bot commands")
                                    .WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild)
                                    .Build();


    public static SlashCommandProperties BuildDisableCommand()
        => new SlashCommandBuilder().WithName(ExplicitCommands.disable.ToString("G"))
                                    .WithDescription("Unregister all bot commands")
                                    .WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild)
                                    .Build();

}
