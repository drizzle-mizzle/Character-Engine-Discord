using Discord;

namespace CharacterEngine.App.Helpers.Discord;


public enum SpecialCommands
{
    start, disable
}


public enum BotAdminCommands
{
    shutdown, blockUser, unblockUser, blockGuild, unblockGuild, reportMetrics
}


public static class ExplicitCommandBuilders
{
    public static SlashCommandProperties BuildStartCommand()
        => new SlashCommandBuilder().WithName(SpecialCommands.start.ToString("G"))
                                    .WithDescription("Register bot commands")
                                    .WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild)
                                    .Build();


    public static SlashCommandProperties BuildDisableCommand()
        => new SlashCommandBuilder().WithName(SpecialCommands.disable.ToString("G"))
                                    .WithDescription("Unregister all bot commands")
                                    .WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild)
                                    .Build();


    public static List<SlashCommandProperties> BuildAdminCommands()
    {
        var commands = new List<SlashCommandProperties>();

        var shutdownCommand = CreateAdminCommand(BotAdminCommands.shutdown).Build();
        commands.Add(shutdownCommand);

        var userOption = new SlashCommandOptionBuilder
        {
            Name = "user-id",
            Description = "-",
            Type = ApplicationCommandOptionType.Integer
        };

        var blockUserCommand = CreateAdminCommand(BotAdminCommands.blockUser).AddOption(userOption).Build();
        commands.Add(blockUserCommand);

        var unblockUserCommand = CreateAdminCommand(BotAdminCommands.unblockUser).AddOption(userOption).Build();
        commands.Add(unblockUserCommand);

        var reportMetricsCommand = CreateAdminCommand(BotAdminCommands.reportMetrics).Build();
        commands.Add(reportMetricsCommand);

        return commands;
    }


    private static string[] GetAdminCommandNames()
        => Enum.GetNames(typeof(BotAdminCommands));


    private static SlashCommandBuilder CreateAdminCommand(BotAdminCommands command)
        => new SlashCommandBuilder().WithName(command.ToString("G").SplitWordsBySep('-').ToLowerInvariant())
                                    .WithDescription("-")
                                    .WithDefaultMemberPermissions(GuildPermission.Administrator);
}
