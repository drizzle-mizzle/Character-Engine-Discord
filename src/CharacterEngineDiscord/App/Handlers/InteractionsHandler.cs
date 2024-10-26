using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using NLog;
using SakuraAi.Client.Exceptions;

namespace CharacterEngine.App.Handlers;


public class InteractionsHandler
{
    private readonly ILogger _log;
    private AppDbContext _db { get; set; }

    private readonly DiscordSocketClient _discordClient;


    public InteractionsHandler(ILogger log, AppDbContext db, DiscordSocketClient discordClient)
    {
        _log = log;
        _db = db;

        _discordClient = discordClient;
    }


    public Task HandleInteraction(ICommandInfo _, IInteractionContext interactionContext, IResult result)
    {
        Task.Run(async () =>
        {
            try
            {
                await HandleInteractionAsync(interactionContext, result);
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync(e);
                await RespondWithErrorAsync(interactionContext, e);
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleInteractionAsync(IInteractionContext interactionContext, IResult result)
    {
        if (result.IsSuccess)
        {
            return;
        }

        var interaction = (ISlashCommandInteraction)interactionContext.Interaction;
        var exception = ((ExecuteResult)result).Exception;


        if (exception is not UserFriendlyException && exception.InnerException is not UserFriendlyException)
        {
            var content = $"Command: {interaction.Data.Name} [ {string.Join(" | ", interaction.Data.Options.Select(opt => $"{opt.Name}: {opt.Value}"))} ]\n" +
                          $"User: {interactionContext.User.Username} ({interactionContext.User.Id})\n" +
                          $"Channel: {interactionContext.Channel.Name} ({interactionContext.Channel.Id})\n" +
                          $"Guild: {interactionContext.Guild.Name} ({interactionContext.Guild.Id})\n\n" +
                          $"Exception:\n{exception}";

            await _discordClient.ReportErrorAsync("Interaction exception", content);
        }

        await RespondWithErrorAsync(interactionContext, exception);
    }


    private static async Task RespondWithErrorAsync(IInteractionContext interactionContext, Exception e)
    {
        var isBold = (e as UserFriendlyException)?.Bold ?? true;
        var exception = e.InnerException ?? e;

        var message = exception is UserFriendlyException or SakuraException // controlled exceptions
                ? exception.Message.ToInlineEmbed(Color.Red, bold: isBold)
                : MessagesTemplates.SOMETHING_WENT_WRONG;

        try
        {
            await interactionContext.Interaction.RespondAsync(embed: message);
        }
        catch
        {
            try
            {
                await interactionContext.Interaction.FollowupAsync(embed: message);
            }
            catch
            {
                try
                {
                    await interactionContext.Interaction.ModifyOriginalResponseAsync(msg => { msg.Embed = message; });
                }
                catch
                {
                    // ...but in the end, it doesn't even matter
                }
            }
        }
    }
}
