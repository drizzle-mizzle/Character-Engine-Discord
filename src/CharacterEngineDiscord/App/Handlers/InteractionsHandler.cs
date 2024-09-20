using CharacterEngine.Helpers.Discord;
using CharacterEngineDiscord.Db;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.Handlers;


public class InteractionsHandler
{
    public required LocalStorage LocalStorage { get; set; }
    public required DiscordSocketClient DiscordClient { get; set; }
    public required AppDbContext db { get; set; }


    public async Task HandleInteractionAsync(ICommandInfo commandInfo, IInteractionContext interactionContext, IResult result)
    {
        if (result.IsSuccess)
        {
            return;
        }

        var interaction = (ISlashCommandInteraction)interactionContext.Interaction;

        var content = $"Command: {interaction.Data.Name} [ {string.Join(" | ", interaction.Data.Options.Select(opt => $"{opt.Name}: {opt.Value}"))} ]\n" +
                      $"User: {interactionContext.User.Username} ({interactionContext.User.Id})\n" +
                      $"Channel: {interactionContext.Channel.Name} ({interactionContext.Channel.Id})\n" +
                      $"Guild: {interactionContext.Guild.Name} ({interactionContext.Guild.Id})\n" +
                      $"Exception:\n {((ExecuteResult)result).Exception}";

        await DiscordClient.ReportErrorAsync("Interaction exception", content);
    }
}
