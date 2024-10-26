using System.Text.RegularExpressions;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Helpers.Integrations;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.SakuraAi;
using Discord;
using NLog;

namespace CharacterEngine.App.Helpers.Discord;

public static class MessagesHelper
{
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    private const int DESC_LIMIT = 4000;


    public const string TP_MSG = "{{msg}}";
    public const string TP_DATETIME = "{{date}}";
    public const string TP_USER = "{{user}}";
    public const string TP_USER_MENTION_HINT = "{{mention_hint}}";

    public const string TP_REF_MSG = "{{ref_msg}}";
    public const string TP_REF_BEGIN = "{{ref_begin}}";
    public const string TP_REF_END = "{{ref_end}}";
    public const string TP_REF_USER = "{{ref_user}}";

    public static readonly Regex USER_MENTION_REGEX = new(@"\<@\d*?\>", RegexOptions.Compiled);
    public static readonly Regex ROLE_MENTION_REGEX = new(@"\<@\&\d*?\>", RegexOptions.Compiled);



    public static Task ReportErrorAsync(this IDiscordClient discordClient, Exception e)
        => discordClient.ReportErrorAsync("Unknown exception", $"{e}");

    public static Task ReportErrorAsync(this IDiscordClient discordClient, string title, Exception e)
        => discordClient.ReportErrorAsync(title, $"{e}");


    private const int MSG_LIMIT = 1990;
    public static async Task ReportErrorAsync(this IDiscordClient discordClient, string title, string content)
    {
        _log.Error($"Error report:\n[ {title} ]\n{content}");

        var channel = (ITextChannel)await discordClient.GetChannelAsync(BotConfig.ERRORS_CHANNEL_ID);
        var thread = await channel.CreateThreadAsync($"💀 {title}", autoArchiveDuration: ThreadArchiveDuration.ThreeDays);

        while (content.Length > 0)
        {
            if (content.Length <= MSG_LIMIT)
            {
                await thread.SendMessageAsync($"```js\n{content}```");
                break;
            }

            await thread.SendMessageAsync(text: $"```js\n{content[..(MSG_LIMIT-1)]}```");
            content = content[MSG_LIMIT..];
        }
    }


    public static async Task ReportLogAsync(this IDiscordClient discordClient, string title, string? content, string? imageUrl = null, Color? color = null)
    {
        _log.Info($"[ {title} ] {content}");

        var channel = (ITextChannel)await discordClient.GetChannelAsync(BotConfig.LOGS_CHANNEL_ID);
        var message = await channel.SendMessageAsync(embed: title.ToInlineEmbed(color ?? Color.Green, false, imageUrl));
        if (content is null)
        {
            return;
        }

        var thread = await channel.CreateThreadAsync("Info", autoArchiveDuration: ThreadArchiveDuration.ThreeDays, message: message);
        while (content.Length > 0)
        {
            if (content.Length <= MSG_LIMIT)
            {
                await thread.SendMessageAsync(content);
                break;
            }

            await thread.SendMessageAsync(content[..(MSG_LIMIT-1)]);
            content = content[MSG_LIMIT..];
        }
    }


    public static Embed ToInlineEmbed(this string text, Color color, bool bold = true, string? imageUrl = null, bool imageAsThumb = false)
    {
        var desc = bold ? $"**{text}**" : text;

        var embed = new EmbedBuilder().WithDescription(desc).WithColor(color);

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return embed.Build();
        }

        if (imageAsThumb)
        {
            embed.WithThumbnailUrl(imageUrl);
        }
        else
        {
            embed.WithImageUrl(imageUrl);
        }

        return embed.Build();
    }


    public static Embed BuildCharacterDescriptionCard(ICharacter character)
    {
        var type = character.GetIntegrationType();
        var embed = new EmbedBuilder();

        var desc = character switch
        {
            ISakuraCharacter s => s.GetSakuraDesc(),
            _ => throw new ArgumentOutOfRangeException(nameof(character), character, null)
        };

        if (desc.Length > DESC_LIMIT)
        {
            desc = desc[..DESC_LIMIT] + " [...]";
        }

        desc += "\n\n";

        var spawnedCharacter = (ISpawnedCharacter)character;
        var details = $"*Call prefix: `{spawnedCharacter.CallPrefix}`*\n" +
                      $"*Original link: [__{type.GetLinkPrefix()} {character.CharacterName}__]({character.GetCharacterLink()})*\n" +
                      $"*{type.GetStatLabel()}: `{character.GetStat()}`*\n" +
                      "*Can generate images: `No`*\n\n";

        var configuration = $"Webhook ID: *`{spawnedCharacter.WebhookId}`*\n" +
                            $"Use it or character's call prefix to modify this integration with *`/conf `* commands.";

        embed.WithColor(type.GetColor());
        embed.WithTitle($"{type.GetIcon()} Character spawned successfully");
        embed.WithDescription($"{desc}");
        embed.AddField("Details", details);
        embed.AddField("Configuration", configuration);

        if (!string.IsNullOrEmpty(character.CharacterImageLink))
        {
            embed.WithImageUrl(character.CharacterImageLink);
        }

        embed.WithFooter($"Created by {character.CharacterAuthor}");

        return embed.Build();
    }


    private static string GetSakuraDesc(this ISakuraCharacter sakuraCharacter)
    {
        var desc = sakuraCharacter.SakuraDescription.Trim(' ', '\n');
        if (desc.Length < 2)
        {
            desc = "*No description*";
        }

        var scenario = sakuraCharacter.SakuraScenario.Trim(' ', '\n');
        if (scenario.Length < 2)
        {
            scenario = "*No scenario*";
        }

        var result = $"**{((ICharacter)sakuraCharacter).CharacterName}**\n{desc}\n\n" +
                     $"**Scenario**\n{scenario}";

        return result;
    }
}
