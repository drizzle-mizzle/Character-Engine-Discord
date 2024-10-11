using CharacterEngine.App.Helpers.Common;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using NLog;
using SakuraAi.Client.Exceptions;
using SakuraAi.Client.Models.Common;

namespace CharacterEngine.App.Handlers;


public class ModalsHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _log;
    private AppDbContext _db { get; }

    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public ModalsHandler(IServiceProvider serviceProvider, ILogger log, AppDbContext db, DiscordSocketClient discordClient, InteractionService interactions)
    {
        _serviceProvider = serviceProvider;
        _log = log;
        _db = db;

        _discordClient = discordClient;
        _interactions = interactions;
    }


    public Task HandleModal(SocketModal modal)
        => Task.Run(async () => await HandleModalAsync(modal));
    

    private async Task HandleModalAsync(SocketModal modal)
    {
        try
        {
            await modal.DeferAsync();
            var parsedModal = InteractionsHelper.ParseCustomId(modal.Data.CustomId);

            await (parsedModal.ActionType switch
            {
                ModalActionType.CreateIntegration => CreateIntegrationAsync(modal, int.Parse(parsedModal.Data))
            });
        }
        catch (Exception e)
        {
            await _discordClient.ReportErrorAsync(e);
        }
    }


    private Task CreateIntegrationAsync(SocketModal modal, int intergrationType)
    {
        return (IntegrationType)intergrationType switch
        {
            IntegrationType.SakuraAI => CreateSakuraAiIntegrationAsync(modal)
        };
    }


    private async Task CreateSakuraAiIntegrationAsync(SocketModal modal)
    {
        // Sending mail
        var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();

        SakuraSignInAttempt attempt;
        try
        {
            attempt = await RuntimeStorage.SakuraAiClient.SendLoginEmailAsync(email);
        }
        catch (SakuraAiException e)
        {
            await modal.FollowupAsync(embed: $"{MessagesTemplates.WARN_SIGN_DISCORD} SakuraAI responded with error:\n```{e.Message}```".ToInlineEmbed(Color.Red));
            throw;
        }

        // Respond to user
        var msg = $"{MessagesTemplates.SAKURA_EMOJI} **SakuraAI**\n\n" +
                  $"Confirmation mail was sent to **{email}**. Please check your mailbox and follow further instructions.\n\n" +
                  $"- *It's recommended to log out of your SakuraAI account in the browser first, before you open a link in the mail; or simply open it in [incognito tab](https://support.google.com/chrome/answer/95464?hl=en&co=GENIE.Platform%3DDesktop&oco=1#:~:text=New%20Incognito%20Window).*\n" +
                  $"- *It may take up to a minute for the bot to react on succeful confirmation.*";

        await modal.FollowupAsync(embed: msg.ToInlineEmbed(bold: false, color: Color.Green));

        // Update db
        var data = StoredActionsHelper.CreateSakuraAiEnsureLoginData(attempt, (ulong)modal.ChannelId!, modal.User.Id);
        var newAction = new StoredAction(StoredActionType.SakuraAiEnsureLogin, data, maxAttemtps: 25);

        await _db.StoredActions.AddAsync(newAction);
        await _db.SaveChangesAsync();
    }

}
