using CharacterEngine.Api.Abstractions;
using CharacterEngine.Helpers;
using CharacterEngine.Helpers.Common;
using CharacterEngine.Helpers.Discord;
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
            var modalData = DiscordModalsHelper.ParseCustomId(modal.Data.CustomId);

            switch (modalData.ActionType)
            {
                case ModalActionType.CreateIntegration:
                {
                    var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();

                    var attempt = await SakuraAiClient.SendLoginEmailAsync(email);
                    await modal.FollowupAsync(embed: $"Confirmation mail was sent to email address **{email}**".ToInlineEmbed(bold: false, color: Color.Green));

                    var data = StoredActionsHelper.CreateSakuraAiEnsureLoginData(attempt, (ulong)modal.ChannelId!, modal.User.Id);
                    var action = new StoredAction(StoredActionType.SakuraAiEnsureLogin, data);
                    await db.StoredActions.AddAsync(action);
                    await db.SaveChangesAsync();

                    return;
                }
            }

        }
        catch (Exception e)
        {
            await DiscordClient.ReportErrorAsync(e);
        }
    }
}
