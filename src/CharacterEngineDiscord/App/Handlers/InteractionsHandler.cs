using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using NLog;

namespace CharacterEngine.App.Handlers;


public class InteractionsHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _log;
    private AppDbContext _db { get; set; }

    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public InteractionsHandler(IServiceProvider serviceProvider, ILogger log, AppDbContext db,
                               DiscordSocketClient discordClient, InteractionService interactions)
    {
        _serviceProvider = serviceProvider;
        _log = log;
        _db = db;

        _discordClient = discordClient;
        _interactions = interactions;
    }


    public Task HandleInteraction(ICommandInfo commandInfo, IInteractionContext interactionContext, IResult result)
        => Task.Run(async () => await HandleInteractionAsync(commandInfo, interactionContext, result));


    private async Task HandleInteractionAsync(ICommandInfo commandInfo, IInteractionContext interactionContext, IResult result)
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

        await _discordClient.ReportErrorAsync("Interaction exception", content);
    }
}
