using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Helpers.Integrations;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using Discord;
using NLog;

namespace CharacterEngine.App.Helpers.Discord;

public static class MessagesHelper
{
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();


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


    public static Embed BuildCharacterDescriptionCard(ISpawnedCharacter spawnedCharacter)
    {
        var type = spawnedCharacter.GetIntegrationType();
        var embed = new EmbedBuilder();

        // var l = Math.Min(commonCharacter.Desc.Length, 4000) - 1;
        // desc += l > 0 ? "[none]" : spawnedCharacter.CharacterDesc;

        var desc = spawnedCharacter switch
        {
            SakuraAiSpawnedCharacter s => s.GetSakuraDesc()
        };

        if (desc.Length >= 4000)
        {
            desc = $"{desc}[...]";
        }

        embed.WithColor(type.GetColor());
        embed.WithTitle($"{type.GetIcon()} Character spawned successfully");
        embed.WithDescription(desc);
        embed.AddField("Configuration", $"Webhook ID: *`{spawnedCharacter.WebhookId}`*\nUse it or character's call prefix to modify this integration with *`/conf `* commands.");

        // TODO: redo
        var details = $"*Call prefix: `{spawnedCharacter.CallPrefix}`*\n" +
                      "*Source: [SakuraAI](https://www.sakura.fm/)*\n" +
                      $"*Original link: [__Chat with {spawnedCharacter.CharacterName}__]({spawnedCharacter.GetIntegrationType().GetCharacterLink(spawnedCharacter.CharacterId)})*\n" +
                      $"*Conversations on [SakuraAI](https://www.sakura.fm/): `{((SakuraAiSpawnedCharacter)spawnedCharacter).SakuraConverstaionsCount}`*\n" +
                      "*Can generate images: `No`*";
        embed.AddField("Details", details);

        if (!string.IsNullOrEmpty(spawnedCharacter.CharacterImageLink))
        {
            embed.WithImageUrl(spawnedCharacter.CharacterImageLink);
        }

        embed.WithFooter($"Created by {spawnedCharacter.CharacterAuthor}");

        return embed.Build();
    }


    private const int DESC_LIMIT = 4000;
    private static string GetSakuraDesc(this SakuraAiSpawnedCharacter sakuraCharacter)
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

        var persona = sakuraCharacter.SakuraPersona.Trim(' ', '\n');
        if (persona.Length < 2)
        {
            persona = "*No persona*";
        }

        var result = $"**{sakuraCharacter.CharacterName}**\n{desc}\n\n" +
                     $"**Scenario**\n{scenario}\n\n" +
                     $"**Persona**\n{persona}";

        if (result.Length > DESC_LIMIT)
        {
            result = result[..DESC_LIMIT] + " [...]";
        }

        return result;
    }
}
