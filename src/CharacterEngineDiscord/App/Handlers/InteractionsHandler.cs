using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
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
    private readonly DiscordSocketClient _discordClient;


    public InteractionsHandler(DiscordSocketClient discordClient)
    {
        _discordClient = discordClient;
    }


    public Task HandleInteraction(ICommandInfo _, IInteractionContext interactionContext, IResult result)
    {
        var traceId = CommonHelper.NewTraceId();
        Task.Run(async () =>
        {
            try
            {
                await HandleInteractionAsync(interactionContext, result, traceId);
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync(e, traceId);
                await RespondWithErrorAsync(interactionContext, e, traceId);
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleInteractionAsync(IInteractionContext interactionContext, IResult result, string traceId)
    {
        if (result.IsSuccess)
        {
            return;
        }

        if (result.Error == InteractionCommandError.UnmetPrecondition)
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

            await _discordClient.ReportErrorAsync("Interaction exception", content, traceId);
        }

        await RespondWithErrorAsync(interactionContext, exception, traceId);
    }


    private static async Task RespondWithErrorAsync(IInteractionContext interactionContext, Exception e, string traceId)
    {
        var isBold = (e as UserFriendlyException)?.Bold ?? true;
        var exception = e.InnerException ?? e;

        var message = exception is UserFriendlyException or SakuraException // controlled exceptions
                ? exception.Message : $"{MessagesTemplates.X_SIGN_DISCORD} Something went wrong!";

        if (!message.StartsWith(MessagesTemplates.X_SIGN_DISCORD) && !message.StartsWith(MessagesTemplates.WARN_SIGN_DISCORD))
        {
            message = $"{MessagesTemplates.X_SIGN_DISCORD} {message}";
        }

        if (isBold)
        {
            message = $"**{message}**";
        }

        var embed = new EmbedBuilder().WithColor(Color.Red)
                                      .WithDescription(message)
                                      .WithFooter($"*Error trace ID: {traceId}*")
                                      .Build();
        try
        {
            await interactionContext.Interaction.RespondAsync(embed: embed);
        }
        catch
        {
            try
            {
                await interactionContext.Interaction.FollowupAsync(embed: embed);
            }
            catch
            {
                try
                {
                    await interactionContext.Interaction.ModifyOriginalResponseAsync(msg => { msg.Embed = embed; });
                }
                catch
                {
                    // ...but in the end, it doesn't even matter
                }
            }
        }
    }
}
