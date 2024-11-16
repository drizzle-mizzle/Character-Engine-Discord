using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord.WebSocket;

namespace CharacterEngine.App.Handlers;


public class ModalsHandler
{
    private readonly DiscordSocketClient _discordClient;


    public ModalsHandler(DiscordSocketClient discordClient)
    {
        _discordClient = discordClient;
    }


    public Task HandleModal(SocketModal modal)
    {
        Task.Run(async () =>
        {
            try
            {
                await HandleModalAsync(modal);
            }
            catch (Exception e)
            {
                var traceId = CommonHelper.NewTraceId();
                await _discordClient.ReportErrorAsync("HandleModal", null, e, traceId, writeMetric: true);
                await InteractionsHelper.RespondWithErrorAsync(modal, e, traceId);
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleModalAsync(SocketModal modal)
    {
        await modal.DeferAsync(ephemeral: true);

        var parsedModal = InteractionsHelper.ParseCustomId(modal.Data.CustomId);

        await (parsedModal.ActionType switch
        {
            ModalActionType.CreateIntegration => CreateIntegrationAsync(modal, int.Parse(parsedModal.Data))
        });
    }


    private static async Task CreateIntegrationAsync(SocketModal modal, int intergrationType)
    {
        var type = (IntegrationType)intergrationType;
        var existingIntegration = await DatabaseHelper.GetGuildIntegrationAsync((ulong)modal.GuildId!, type);
        if (existingIntegration is not null)
        {
            throw new UserFriendlyException($"This server already has {type.GetIcon()}{type:G} integration");
        }

        await (type switch
        {
            IntegrationType.SakuraAI => ModalsHelper.CreateSakuraAiIntegrationAsync(modal),
            IntegrationType.CharacterAI => ModalsHelper.CreateCharacterAiIntegrationAsync(modal),
        });
    }

}
