using CharacterEngine.App.Helpers.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using NLog;

namespace CharacterEngine.App.Helpers.Discord;

public static class MessagesHelper
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();


    public static Task ReportErrorAsync(this IDiscordClient discordClient, Exception e)
        => discordClient.ReportErrorAsync("Unknown exception", $"{e}");

    public static Task ReportErrorAsync(this IDiscordClient discordClient, string title, Exception e)
        => discordClient.ReportErrorAsync(title, $"{e}");


    private const int LIMIT = 1990;
    public static async Task ReportErrorAsync(this IDiscordClient discordClient, string title, string content)
    {
        _log.Error($"Error report: [ {title} ]\n{content}");

        var channel = (ITextChannel)await discordClient.GetChannelAsync(BotConfig.ERRORS_CHANNEL_ID);
        var thread = await channel.CreateThreadAsync($"💀 {title}", autoArchiveDuration: ThreadArchiveDuration.ThreeDays);

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
            if (content.Length <= LIMIT)
            {
                await thread.SendMessageAsync(content);
                break;
            }

            await thread.SendMessageAsync(content[..(LIMIT-1)]);
            content = content[LIMIT..];
        }
    }


    public static Embed ToInlineEmbed(this string text, Color color, bool bold = true, string? imageUrl = null, bool imageAsThumb = false)
    {
        var desc = bold ? $"**{text}**" : text;

        var embed = new EmbedBuilder().WithDescription(desc).WithColor(color);
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            if (imageAsThumb)
            {
                embed.WithThumbnailUrl(imageUrl);
            }
            else
            {
                embed.WithImageUrl(imageUrl);
            }
        }

        return embed.Build();
    }


    public static Embed BuildCharacterDescriptionCard(ISpawnedCharacter spawnedCharacter, CommonCharacter commonCharacter)
    {
        var type = commonCharacter.IntegrationType;
        var embed = new EmbedBuilder().WithTitle($"{type:G}").WithColor(type.GetColor());
        var l = Math.Min(commonCharacter.Desc.Length, 4000) - 1;


        var desc = "**Description**\n";
        desc += l > 0 ? "[none]" : spawnedCharacter.CharacterDesc;
        if (spawnedCharacter.CharacterDesc.Length >= 4000)
        {
            desc = $"{desc}[...]";
        }

        embed.WithDescription(desc);

        return embed.Build();
    }

}
