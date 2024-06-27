using Discord;

namespace CharacterEngine.Helpers.Discord;


public static class MessagesTemplates
{
    public const string OK_SIGN_DISCORD = ":white_check_mark: ";
    public const string WARN_SIGN_UNICODE = "⚠";
    public const string WARN_SIGN_DISCORD = ":warning:";

    public const string SAKURA_EMOJI = "<:sakura128:1254405302099644539>";

    public static readonly Emoji ARROW_LEFT = new("\u2B05");
    public static readonly Emoji ARROW_RIGHT = new("\u27A1");
    public static readonly Emoji STOP_BTN = new("\u26D4");
    public static readonly Emoji CRUTCH_BTN = new("\ud83e\ude7c");

    public static readonly Embed WAIT_MESSAGE = "🕓 Wait...".ToInlineEmbed(new Color(154, 171, 182));
    public static readonly Embed CHARACTER_NOT_FOUND_MESSAGE = $"{WARN_SIGN_DISCORD} Character with the given call prefix or webhook ID was not found in the current channel".ToInlineEmbed(Color.Red);

}
