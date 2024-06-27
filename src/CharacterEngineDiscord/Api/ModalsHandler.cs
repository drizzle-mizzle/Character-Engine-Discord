using CharacterEngine.Api.Abstractions;
using CharacterEngine.Helpers;
using CharacterEngine.Helpers.Common;
using CharacterEngine.Helpers.Discord;
using CharacterEngine.Models;
using CharacterEngine.Models.Db;
using Discord;
using Discord.WebSocket;
using SakuraAi;
using static CharacterEngine.Models.Enums;

namespace CharacterEngine.Api;


public class ModalsHandler : HandlerBase
{
    public static async Task HandleModalAsync(SocketModal modal)
    {
        try
        {
            await modal.DeferAsync();
            var parsedModal = DiscordModalsHelper.ParseCustomId(modal.Data.CustomId);

            switch (parsedModal.ActionType)
            {
                case ModalActionType.CreateIntegration:
                    await CreateIntegrationAsync(modal, (IntegrationType)int.Parse(parsedModal.Data)); break;
            };

        }
        catch (Exception e)
        {
            await DiscordClient.ReportErrorAsync(e);
        }
    }


    private static async Task CreateIntegrationAsync(SocketModal modal, IntegrationType intergrationType)
    {
        switch (intergrationType)
        {
            case IntegrationType.SakuraAi: await CreateSakuraAiIntegrationAsync(modal); break;
        };
    }


    private static async Task CreateSakuraAiIntegrationAsync(SocketModal modal)
    {
        var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();

        var attempt = await SakuraAiClient.SendLoginEmailAsync(email);
        await modal.FollowupAsync(embed: $"{MessagesTemplates.SAKURA_EMOJI} Confirmation mail was sent to **{email}**".ToInlineEmbed(bold: false, color: Color.Green));

        var data = StoredActionsHelper.CreateSakuraAiEnsureLoginData(attempt, (ulong)modal.ChannelId!, modal.User.Id);
        var action = new StoredAction(StoredActionType.SakuraAiEnsureLogin, data);

        await db.StoredActions.AddAsync(action);
        await db.SaveChangesAsync();
    }
}
