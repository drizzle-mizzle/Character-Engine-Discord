using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.CustomAttributes;


public enum AccessLevels
{
    BotAdmin,
    GuildAdmin,
    Manager
}


public class ValidateAccessLevelAttribute : PreconditionAttribute
{
    private readonly AccessLevels _requiredAccessLevel;

    public ValidateAccessLevelAttribute(AccessLevels accessLevel)
    {
        _requiredAccessLevel = accessLevel;
    }


    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo info, IServiceProvider services)
    {
        await InteractionsHelper.ValidateAccessAsync(_requiredAccessLevel, (SocketGuildUser)context.User);
        return PreconditionResult.FromSuccess();
    }
}


public class DeferAndValidatePermissionsAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo info, IServiceProvider services)
    {
        if (context.Channel is not ITextChannel channel)
        {
            throw new UserFriendlyException($"{MessagesTemplates.WARN_SIGN_DISCORD} Bot can opearte only in text channels");
        }

        await context.Interaction.DeferAsync();

        var validationResult = await WatchDog.ValidateAsync(context.User.Id, context.Guild.Id);
        if (validationResult is not WatchDogValidationResult.Passed)
        {
            _ = await context.Interaction.FollowupAsync();
            return PreconditionResult.FromError("Blocked");
        }

        await channel.EnsureExistInDbAsync();
        await InteractionsHelper.ValidatePermissionsAsync((IGuildChannel)context.Channel);

        return PreconditionResult.FromSuccess();
    }

}
