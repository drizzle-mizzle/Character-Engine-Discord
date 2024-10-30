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
        await InteractionsHelper.ValidateChannelPermissionsAsync((IGuildChannel)context.Channel);
        return PreconditionResult.FromSuccess();
    }
}
