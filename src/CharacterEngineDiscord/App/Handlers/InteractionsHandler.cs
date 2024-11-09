using CharacterAi.Client.Exceptions;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
            catch (SakuraException)
            {
                // care not
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync(e, traceId);
                await InteractionsHelper.RespondWithErrorAsync(interactionContext.Interaction, e, traceId);
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
        if (result is not ExecuteResult executeResult)
        {
            return;
        }

        var exception = executeResult.Exception;

        if (exception is not UserFriendlyException && exception.InnerException is not UserFriendlyException)
        {
            string content;

            if (interaction.Data.Options.Any(opt => opt.Type is ApplicationCommandOptionType.SubCommand))
            {
                var subCommand = interaction.Data.Options.First();
                content = $"Command: {interaction.Data.Name}/{subCommand.Name} [ {string.Join(" | ", subCommand.Options.Select(opt => $"{opt.Name}: {opt.Value}"))} ]\n";
            }
            else if (interaction.Data.Options.Any(opt => opt.Type is ApplicationCommandOptionType.SubCommandGroup))
            {
                var subCommandGroup = interaction.Data.Options.First();
                var subCommand = subCommandGroup.Options.First();
                content = $"Command: {interaction.Data.Name}/{subCommandGroup.Name}/{subCommand.Name} [ {string.Join(" | ", subCommand.Options.Select(opt => $"{opt.Name}: {opt.Value}"))} ]\n";
            }
            else
            {
                content = $"Command: {interaction.Data.Name} [ {string.Join(" | ", interaction.Data.Options.Select(opt => $"{opt.Name}: {opt.Value}"))} ]\n";
            }

            var owner = await interactionContext.Guild.GetOwnerAsync();
            content += $"User: **{interactionContext.User.Username}** ({interactionContext.User.Id})\n" +
                       $"Channel: **{interactionContext.Channel.Name}** ({interactionContext.Channel.Id})\n" +
                       $"Guild: **{interactionContext.Guild.Name}** ({interactionContext.Guild.Id})\n" +
                       $"Owned by: **{owner.DisplayName ?? owner.Username}** ({owner.Id})\n\n" +
                       $"Exception:\n```cs{exception}```";

            await _discordClient.ReportErrorAsync("Interaction exception", content, traceId);
        }

        await InteractionsHelper.RespondWithErrorAsync(interactionContext.Interaction, exception, traceId);
    }

}
