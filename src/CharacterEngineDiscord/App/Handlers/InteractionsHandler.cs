using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.Handlers;


public class InteractionsHandler
{
    private readonly IServiceProvider _serviceProvider;
    private AppDbContext _db { get; set; }
    private readonly LocalStorage _localStorage;
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public InteractionsHandler(IServiceProvider serviceProvider, AppDbContext db, LocalStorage localStorage, DiscordSocketClient discordClient, InteractionService interactions)
    {
        _serviceProvider = serviceProvider;
        _db = db;

        _localStorage = localStorage;
        _discordClient = discordClient;
        _interactions = interactions;
    }


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

        await _discordClient.ReportErrorAsync("Interaction exception", content);
    }
}
