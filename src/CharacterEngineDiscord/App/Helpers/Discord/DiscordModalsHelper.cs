using CharacterEngineDiscord.Models;
using Discord;
using NLog;

namespace CharacterEngine.App.Helpers.Discord;


public static class DiscordModalsHelper
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    private const string SEP = "%data~sep%";


    public static Modal BuildSakuraAiAuthModal(this ModalBuilder modalBuilder)
    {
        modalBuilder.AddTextInput("Email", "email", placeholder: "what@bipki.com", required: true, minLength: 2, maxLength: 128);

        return modalBuilder.Build();
    }


    public static string NewCustomId(Enums.ModalActionType action, string data)
        => NewCustomId(Guid.NewGuid(), action, data);

    public static string NewCustomId(Guid id, Enums.ModalActionType action, string data)
        => $"{id}{SEP}{action}{SEP}{data}";


    public static ModalData ParseCustomId(string customId)
    {
        var parts = customId.Split(SEP);
        return new ModalData(Guid.Parse(parts[0]), (Enums.ModalActionType)Enum.Parse(typeof(Enums.ModalActionType), parts[1]), parts[2]);
    }
}
