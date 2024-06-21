using CharacterEngine.Helpers.Discord;
using Discord;
using Discord.Interactions;

namespace CharacterEngine.Api;


public class InteractionsHandler
{
    public static Task HandleInteraction(ICommandInfo commandInfo, IInteractionContext interactionContext, IResult result)
    {
        if (result.IsSuccess)
        {
            return Task.CompletedTask;
        }

        var interaction = (ISlashCommandInteraction)interactionContext.Interaction;

        string content = $"Command: {interaction.Data.Name} [ {string.Join(" | ", interaction.Data.Options.Select(opt => $"{opt.Name}: {opt.Value}"))} ]\n" +
                         $"User: {interactionContext.User.Username} ({interactionContext.User.Id})\n" +
                         $"Channel: {interactionContext.Channel.Name} ({interactionContext.Channel.Id})\n" +
                         $"Guild: {interactionContext.Guild.Name} ({interactionContext.Guild.Id})\n" +
                         $"Exception:\n {((ExecuteResult)result).Exception}";

        return interactionContext.Client.ReportErrorAsync("Interaction exception", content);
    }
}
