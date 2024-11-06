using Discord;
using NLog;

namespace CharacterEngine.App.Helpers.Discord;


public static class ModalsHelper
{
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();


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

}
