using CharacterEngine.App.Helpers.Discord;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.CustomAttributes;


public enum AccessLevels
{
    BotAdmin,
    GuildAdmin,
    Manager
}


/// <inheritdoc />
public class ValidateAccessLevelAttribute : PreconditionAttribute
{
    private readonly AccessLevels _requiredAccessLevel;

    public ValidateAccessLevelAttribute(AccessLevels accessLevel)
    {
        _requiredAccessLevel = accessLevel;
    }


    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo info, IServiceProvider services)
    {
        await InteractionsHelper.ValidateAccessLevelAsync(_requiredAccessLevel, (SocketGuildUser)context.User);

        return PreconditionResult.FromSuccess();
    }
}


/// <inheritdoc />
public class ValidateChannelPermissionsAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo info, IServiceProvider services)
    {
        if (!IsNoWarnCommand((SocketSlashCommand)context.Interaction))
        {
            await InteractionsHelper.ValidateChannelPermissionsAsync((IGuildChannel)context.Channel);
        }

        return PreconditionResult.FromSuccess();
    }


    private static bool IsNoWarnCommand(SocketSlashCommand command)
        => command.CommandName.StartsWith("channel", StringComparison.Ordinal)
        && command.Data.Options.All(opt => opt.Name is string optName && optName.StartsWith("no-warn", StringComparison.Ordinal));
}
