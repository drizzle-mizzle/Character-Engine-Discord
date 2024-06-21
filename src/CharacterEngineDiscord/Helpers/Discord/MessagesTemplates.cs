using Discord;

namespace CharacterEngine.Helpers.Discord;


public static class MessagesTemplates
{
    public static Embed WAIT_MESSAGE { get; } = "🕓 Wait...".ToInlineEmbed(new Color(154, 171, 182));
}
