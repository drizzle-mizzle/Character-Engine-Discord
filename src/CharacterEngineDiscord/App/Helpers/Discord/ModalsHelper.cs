using CharacterAi.Client.Exceptions;
using CharacterEngineDiscord.IntegrationModules;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.WebSocket;
using NLog;

namespace CharacterEngine.App.Helpers.Discord;


public static class ModalsHelper
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();


    public static Modal BuildSakuraAiAuthModal(this ModalBuilder modalBuilder)
    {
        modalBuilder.AddTextInput("Account email", "email", placeholder: "yourmail@bipki.com", required: true, minLength: 2, maxLength: 128);

        return modalBuilder.Build();
    }


    public static Modal BuildCaiAiAuthModal(this ModalBuilder modalBuilder)
    {
        modalBuilder.AddTextInput("Account email", "email", placeholder: "yourmail@bipki.com", required: true, minLength: 2, maxLength: 128);

        return modalBuilder.Build();
    }


     public static async Task CreateSakuraAiIntegrationAsync(SocketModal modal)
    {
        var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();
        await InteractionsHelper.SendSakuraAiMailAsync(modal, email);
    }


    public static async Task CreateCharacterAiIntegrationAsync(SocketModal modal)
    {
        var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();
        await InteractionsHelper.SendCharacterAiMailAsync(modal, email);
    }
}
