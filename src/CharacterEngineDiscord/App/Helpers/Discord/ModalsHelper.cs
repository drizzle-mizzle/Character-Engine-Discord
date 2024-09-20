using CharacterEngineDiscord.Models;
using Discord;
using NLog;

namespace CharacterEngine.App.Helpers.Discord;


public static class ModalsHelper
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();


    public static Modal BuildSakuraAiAuthModal(this ModalBuilder modalBuilder)
    {
        modalBuilder.AddTextInput("Email", "email", placeholder: "what@bipki.com", required: true, minLength: 2, maxLength: 128);

        return modalBuilder.Build();
    }


    public static ModalData ParseCustomId(string customId)
    {
        var parts = customId.Split(InteractionsHelper.SEP);
        return new ModalData(Guid.Parse(parts[0]), Enum.Parse<ModalActionType>(parts[1]), parts[2]);
    }
}
