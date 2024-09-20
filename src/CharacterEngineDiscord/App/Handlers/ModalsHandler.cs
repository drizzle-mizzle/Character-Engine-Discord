using CharacterEngine.Helpers.Common;
using CharacterEngine.Helpers.Discord;
using CharacterEngineDiscord.Db;
using CharacterEngineDiscord.Db.Models;
using CharacterEngineDiscord.Db.Models.Db;
using Discord;
using Discord.WebSocket;
using SakuraAi;

namespace CharacterEngine.App.Handlers;


public class ModalsHandler
{
    public required LocalStorage LocalStorage { get; set; }
    public required DiscordSocketClient DiscordClient { get; set; }
    public required AppDbContext db { get; set; }
    public required SakuraAiClient SakuraAiClient { get; set; }


    public async Task HandleModalAsync(SocketModal modal)
    {
        try
        {
            await modal.DeferAsync();
            var parsedModal = DiscordModalsHelper.ParseCustomId(modal.Data.CustomId);

            switch (parsedModal.ActionType)
            {
                case Enums.ModalActionType.CreateIntegration:
                {
                    await CreateIntegrationAsync(modal, (Enums.IntegrationType)int.Parse(parsedModal.Data)); break;
                }
            };

        }
        catch (Exception e)
        {
            await DiscordClient.ReportErrorAsync(e);
        }
    }


    private async Task CreateIntegrationAsync(SocketModal modal, Enums.IntegrationType intergrationType)
    {
        switch (intergrationType)
        {
            case Enums.IntegrationType.SakuraAi: await CreateSakuraAiIntegrationAsync(modal); break;
        };
    }


    private async Task CreateSakuraAiIntegrationAsync(SocketModal modal)
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
