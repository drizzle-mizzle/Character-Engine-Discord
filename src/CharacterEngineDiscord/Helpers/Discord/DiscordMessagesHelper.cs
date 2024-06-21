using Discord;
using Discord.WebSocket;
using NLog;

namespace CharacterEngine.Helpers.Discord;

public static class DiscordMessagesHelper
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();


    public static Embed ToInlineEmbed(this string text, Color color, bool bold = true, string? imageUrl = null)
    {
        string desc = bold ? $"**{text}**" : text;

        var embed = new EmbedBuilder().WithDescription(desc).WithColor(color);
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            embed.WithImageUrl(imageUrl);
        }

        return embed.Build();
    }


    public static Task ReportErrorAsync(this IDiscordClient discordClient, Exception e)
        => discordClient.ReportErrorAsync("Unknown exception", $"{e}");

    public static Task ReportErrorAsync(this IDiscordClient discordClient, string title, Exception e)
        => discordClient.ReportErrorAsync(title, $"{e}");


    private const int LIMIT = 1990;
    public static async Task ReportErrorAsync(this IDiscordClient discordClient, string title, string content)
    {
        var channel = (ITextChannel)await discordClient.GetChannelAsync(BotConfig.ERRORS_CHANNEL_ID);
        _log.Error($"Error report: [ {title} ] {content}");

        var thread = await channel.CreateThreadAsync(title, autoArchiveDuration: ThreadArchiveDuration.ThreeDays);
        while (content.Length > 0)
        {
            if (content.Length <= LIMIT)
            {
                await thread.SendMessageAsync($"```js\n{content}```");
                break;
            }

            await thread.SendMessageAsync(text: $"```js\n{content[..(LIMIT-1)]}```");
            content = content[LIMIT..];
        }
    }


    public static async Task ReportLogAsync(IDiscordClient discordClient, string title, string content, uint colorHex = 2067276U)
    {
        _log.Info($"[ {title} ] {content}");

        var channel = (SocketTextChannel)await discordClient.GetChannelAsync(BotConfig.LOGS_CHANNEL_ID);
        var embed = new EmbedBuilder().WithColor(new Color(colorHex)).WithTitle(title).WithDescription(content).Build();

        await channel.SendMessageAsync(embed: embed);
    }

}
