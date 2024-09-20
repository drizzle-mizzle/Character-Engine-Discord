using CharacterEngine.App.Helpers.Common;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.WebSocket;
using SakuraAi.Client;

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
            var parsedModal = ModalsHelper.ParseCustomId(modal.Data.CustomId);

            await (parsedModal.ActionType switch
            {
                ModalActionType.CreateIntegration
                    => CreateIntegrationAsync(modal, int.Parse(parsedModal.Data))
            });
        }
        catch (Exception e)
        {
            await DiscordClient.ReportErrorAsync(e);
        }
    }


    private Task CreateIntegrationAsync(SocketModal modal, int intergrationType)
    {
        return (IntegrationType)intergrationType switch
        {
            IntegrationType.SakuraAi => CreateSakuraAiIntegrationAsync(modal)
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
