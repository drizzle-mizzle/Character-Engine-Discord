using Discord;
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


    public static Modal BuildOpenRouterAuthModal(this ModalBuilder modalBuilder)
    {
        modalBuilder.AddTextInput("API key", "api-key", required: true, placeholder: "sk-or-v1-0000000000000000000000000000000000000000000000000000000000000069");
        modalBuilder.AddTextInput("Default model", "model", required: false, value: "mistralai/mistral-7b-instruct:free");

        return modalBuilder.Build();
    }
}
