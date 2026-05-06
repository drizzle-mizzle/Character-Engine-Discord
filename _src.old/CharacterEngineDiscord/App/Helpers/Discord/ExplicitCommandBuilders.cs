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


    public static SlashCommandBuilder AddUserOption(this SlashCommandBuilder builder)
        => builder.AddOption(new SlashCommandOptionBuilder()
                            .WithName("user-id")
                            .WithDescription("User ID")
                            .WithType(ApplicationCommandOptionType.String));

    public static SlashCommandBuilder AddDtRangeOptions(this SlashCommandBuilder builder)
        => builder.AddOption(new SlashCommandOptionBuilder()
                            .WithName("range")
                            .WithRequired(false)
                            .WithDescription("range")
                            .WithType(ApplicationCommandOptionType.Integer))
                  .AddOption(new SlashCommandOptionBuilder()
                            .WithName("range-type")
                            .WithRequired(true)
                            .WithDescription("range type")
                            .AddChoice("all-time", 0)
                            .AddChoice("minutes", 1)
                            .AddChoice("hours", 2)
                            .AddChoice("days", 3)
                            .WithType(ApplicationCommandOptionType.Integer));

    public static SlashCommandProperties[] BuildAdminCommands()
    {
        var commands = new List<SlashCommandBuilder>();

        commands.Add(CreateAdminCommand(BotAdminCommands.shutdown));
        commands.Add(CreateAdminCommand(BotAdminCommands.blockUser).AddUserOption());
        commands.Add(CreateAdminCommand(BotAdminCommands.unblockUser).AddUserOption());
        commands.Add(CreateAdminCommand(BotAdminCommands.reportMetrics).AddDtRangeOptions());

        return commands.Select(c => c.Build()).ToArray();
    }


    private static string[] GetAdminCommandNames()
        => Enum.GetNames(typeof(BotAdminCommands));


    private static SlashCommandBuilder CreateAdminCommand(BotAdminCommands command)
        => new SlashCommandBuilder().WithName(command.ToString("G").SplitWordsBySep('-').ToLowerInvariant())
                                    .WithDescription(command.ToString("G").SplitWordsBySep(' ').CapitalizeFirst())
                                    .WithDefaultMemberPermissions(GuildPermission.Administrator);
}
