using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using static CharacterEngine.App.Helpers.Discord.ValidationsHelper;

namespace CharacterEngine.App.CustomAttributes;



/// <inheritdoc />
public class ValidateAccessLevelAttribute : PreconditionAttribute
{
    private readonly AccessLevel _requiredAccessLevel;

    public ValidateAccessLevelAttribute(AccessLevel accessLevel)
    {
        _requiredAccessLevel = accessLevel;
    }


    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo info, IServiceProvider services)
    {
        await ValidateAccessLevelAsync(_requiredAccessLevel, (SocketGuildUser)context.User);

        return PreconditionResult.FromSuccess();
    }
}


/// <inheritdoc />
public class ValidateChannelPermissionsAttribute : PreconditionAttribute
{

    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo info, IServiceProvider services)
    {
        if (IsNoWarnCommand((SocketSlashCommand)context.Interaction))
        {
            return PreconditionResult.FromSuccess();
        }

        await ValidateChannelPermissionsAsync(context.Channel);

        return PreconditionResult.FromSuccess();
    }


    private static bool IsNoWarnCommand(SocketSlashCommand command)
        => command.CommandName.StartsWith("channel", StringComparison.Ordinal)
        && command.Data.Options.All(opt => opt.Name is string optName && optName.StartsWith("no-warn", StringComparison.Ordinal));
}
