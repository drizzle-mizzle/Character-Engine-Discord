using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.WebSocket;
using NLog;

namespace CharacterEngine.App.Handlers;


public class ModalsHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _log;
    private AppDbContext _db { get; }

    private readonly DiscordSocketClient _discordClient;


    public ModalsHandler(IServiceProvider serviceProvider, ILogger log, AppDbContext db, DiscordSocketClient discordClient)
    {
        _serviceProvider = serviceProvider;
        _log = log;
        _db = db;

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
                await _discordClient.ReportErrorAsync(e, CommonHelper.NewTraceId());
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleModalAsync(SocketModal modal)
    {
        await modal.DeferAsync();

        var parsedModal = InteractionsHelper.ParseCustomId(modal.Data.CustomId);

        await (parsedModal.ActionType switch
        {
            ModalActionType.CreateIntegration => CreateIntegrationAsync(modal, int.Parse(parsedModal.Data))
        });
    }


    private Task CreateIntegrationAsync(SocketModal modal, int intergrationType)
    {
        return (IntegrationType)intergrationType switch
        {
            IntegrationType.SakuraAI => InteractionsHelper.CreateSakuraAiIntegrationAsync(modal)
        };
    }

}
