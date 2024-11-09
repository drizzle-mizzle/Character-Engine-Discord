using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Helpers.Mappings;
using CharacterEngine.App.Static;
using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Models.Abstractions.SakuraAi;
using Discord;
using Discord.WebSocket;
using NLog;

namespace CharacterEngine.App.Helpers.Discord;

public static class MessagesHelper
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    private const int DESC_LIMIT = 4000;


    public const string MF_MSG = "{{msg}}";
    public const string MF_DATETIME = "{{date}}";
    public const string MF_USER = "{{user}}";
    public const string MF_USER_MENTION_HINT = "{{mention_hint}}";

    public const string MF_REF_MSG = "{{ref_msg}}";
    public const string MF_REF_BEGIN = "{{ref_begin}}";
    public const string MF_REF_END = "{{ref_end}}";
    public const string MF_REF_USER = "{{ref_user}}";

    public static readonly Regex USER_MENTION_REGEX = new(@"\<@\d*?\>", RegexOptions.Compiled);
    public static readonly Regex ROLE_MENTION_REGEX = new(@"\<@\&\d*?\>", RegexOptions.Compiled);


    #region Reports

    public static Task ReportErrorAsync(this IDiscordClient discordClient, Exception e, string traceId)
        => discordClient.ReportErrorAsync("Unknown exception", $"{e}", traceId);

    public static Task ReportErrorAsync(this IDiscordClient discordClient, string title, Exception e, string traceId)
        => discordClient.ReportErrorAsync(title, $"{e}", traceId);


    private const int MSG_LIMIT = 1990;
    public static async Task ReportErrorAsync(this IDiscordClient discordClient, string title, string content, string traceId, bool consoleLog = true)
    {
        if (consoleLog)
        {
            _log.Error($"[{traceId}] Error report:\n[ {title} ]\n{content}");
        }

        var channel = (ITextChannel)await discordClient.GetChannelAsync(BotConfig.ERRORS_CHANNEL_ID);
        var thread = await channel.CreateThreadAsync($"[{traceId}]💀 {title}", autoArchiveDuration: ThreadArchiveDuration.ThreeDays);

        var count = 0;
        while (content.Length > 0)
        {
            if (count == 5)
            {
                break;
            }

            count++;
            if (content.Length <= MSG_LIMIT)
            {
                await thread.SendMessageAsync($"```js\n{content}```");
                break;
            }

            await thread.SendMessageAsync(text: $"```js\n{content[..(MSG_LIMIT-1)]}```");
            content = content[MSG_LIMIT..];
        }
    }


    public static async Task ReportLogAsync(this IDiscordClient discordClient, string title, string? content = null, string? imageUrl = null, Color? color = null)
    {
        _log.Info($"[ {title} ] {content}");

        var channel = (ITextChannel)await discordClient.GetChannelAsync(BotConfig.LOGS_CHANNEL_ID);
        var message = await channel.SendMessageAsync(embed: title.ToInlineEmbed(color ?? Color.Green, false, imageUrl, imageAsThumb: true));
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

    #endregion


    public static Embed BuildSearchResultList(SearchQuery searchQuery)
    {
        var type = searchQuery.IntegrationType;
        var embed = new EmbedBuilder().WithColor(type.GetColor());

        var title = $"{type.GetIcon()} {type:G}";
        var listTitle = $"({searchQuery.Characters.Count}) Characters found by query **\"{searchQuery.OriginalQuery}\"**:";
        embed.AddField(title, listTitle);

        var pageMultiplier = (searchQuery.CurrentPage - 1) * 10;
        var rows = Math.Min(searchQuery.Characters.Count - pageMultiplier, 10);

        for (var row = 1; row <= rows; row++)
        {
            var characterNumber = row + pageMultiplier;
            var character = searchQuery.Characters.ElementAt(characterNumber - 1);

            var rowTitle = $"{characterNumber}. {character.CharacterName}";
            var rowContent = $"{type.GetStatLabel()}: {character.CharacterStat} **|** [[__character link__]({character.GetCharacterLink()})] **|** Author: [[__{character.CharacterAuthor}__]({character.GetAuthorLink()})]";
            if (searchQuery.CurrentRow == row)
            {
                rowTitle += " - ✅";
            }

            embed.AddField(rowTitle, rowContent);
        }

        embed.WithFooter($"Page {searchQuery.CurrentPage}/{searchQuery.Pages}");

        return embed.Build();
    }


    public static Embed BuildCharactersList(ICollection<ISpawnedCharacter> characters, bool inGuild)
    {
        var embed = new EmbedBuilder().WithColor(Color.Gold);
        embed.WithTitle(inGuild ? "Characters found on this server" : "Characters found in current channel:");

        var listString = new StringBuilder(characters.Count);
        for (var i = 0; i < characters.Count; i++)
        {
            var character = characters.ElementAt(i);
            var line = $"{i + 1}. {character.CharacterName} (*{character.CallPrefix}*) {character.GetIntegrationType().GetIcon()} | ";
            line += inGuild ? $"Channel: **{character.DiscordChannel!.ChannelName}** | MS: {character.MessagesSent}" : $"Messages sent: {character.MessagesSent}";

            listString.AppendLine(line);
        }

        embed.WithDescription(listString.ToString());
        embed.WithFooter($"Amount: {characters.Count}/15");

        return embed.Build();
    }


    public static string BuildMessageFormatPreview(string messagesFormat)
    {
        var userMessage = (authorName: "Cool_AI_Enjoyer69", authorMention: "<@946084133308874783>", content: "What is bipki?");
        var refMessage = (authorName: "Dude", content: "Bipki");
        var formated = BringMessageToFormat(messagesFormat, null, userMessage, refMessage);

        return $"Time: ***`{DateTime.Now.ToString("hh:mm dd-MMM-yyyy", new CultureInfo("en-US"))}`***\n" +
               $"Referenced message: *`\"{refMessage.content}\"`* from user **`{refMessage.authorName}`**\n" +
               $"User message: *`\"{userMessage.content}\"`* from user **`{userMessage.authorName}`**\n" +
               $"Result (what character will see):\n```{formated}```";
    }


    #region Description cards

    public static async Task<Embed> BuildCharacterDescriptionCardAsync(ISpawnedCharacter spawnedCharacter, bool justSpawned)
    {
        var type = spawnedCharacter.GetIntegrationType();
        var embed = new EmbedBuilder();

        var desc = spawnedCharacter switch
        {
            ISakuraCharacter sc => sc.GetSakuraDesc(),
            ICaiCharacter cc => await cc.GetCaiDescAsync(),

            _ => throw new ArgumentOutOfRangeException(nameof(spawnedCharacter), spawnedCharacter, null)
        };

        if (desc.Length > DESC_LIMIT)
        {
            desc = desc[..DESC_LIMIT] + " [...]";
        }

        desc += "\n\n";

        var details = $"*Call prefix: `{spawnedCharacter.CallPrefix}`*\n" +
                      $"*Original link: [__{type.GetLinkPrefix()} {spawnedCharacter.CharacterName}__]({spawnedCharacter.GetCharacterLink()})*\n" +
                      "*Can generate images: `No`*\n" +
                      $"*NSFW: `{spawnedCharacter.IsNfsw}`*\n\n";

        var configuration = $"Webhook ID: *`{spawnedCharacter.WebhookId}`*\n";

        if (justSpawned)
        {
            configuration += "Use it or character's call prefix to modify this integration with *`/character`* commands.\n**Example:**\n*`/character edit any-identifier:@ai data-to-edit:call-prefix new-value:.ai`*";
        }
        else
        {
            configuration += GetConfigurationInfo(spawnedCharacter);
        }

        embed.WithColor(type.GetColor());
        embed.WithTitle($"{type.GetIcon()} Character spawned successfully");
        embed.WithDescription($"{desc}");
        embed.AddField("Details", details);
        embed.AddField("Configuration", configuration);

        if (!string.IsNullOrEmpty(spawnedCharacter.CharacterImageLink))
        {
            embed.WithImageUrl(spawnedCharacter.CharacterImageLink);
        }

        embed.WithFooter($"Created by {spawnedCharacter.CharacterAuthor}");

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

        var result = $"**{sakuraCharacter.CharacterName}**\n{desc}\n\n" +
                     $"**Scenario**\n{scenario}";

        return result;
    }

    private static async Task<string> GetCaiDescAsync(this ICaiCharacter caiCharacter)
    {
        var asSpawnedCharacter = (ISpawnedCharacter)caiCharacter;

        var guildIntegration = (await DatabaseHelper.GetGuildIntegrationAsync(asSpawnedCharacter))!;
        var fullCharacter = await MemoryStorage.IntegrationModules.CaiModule.GetFullCaiChararcterInfoAsync(caiCharacter.CharacterId, guildIntegration);
        asSpawnedCharacter.FillWith(fullCharacter);

        var title = caiCharacter.CaiTitle.Trim(' ', '\n');
        if (title.Length < 2)
        {
            title = "*No title*";
        }

        var desc = caiCharacter.CaiDescription.Trim(' ', '\n');
        if (desc.Length < 2)
        {
            desc = "*No description*";
        }

        var result = $"**{caiCharacter.CharacterName}**\n{title}\n\n" +
                     $"**Description**\n{desc}";

        return result;
    }


    private static string GetConfigurationInfo(this ISpawnedCharacter spawnedCharacter)
    {
        var info = // $"*Response delay: `{spawnedCharacter.ResponseDelay}`*\n" + // TODO: accumulations
                $"*Freewill factor: `{spawnedCharacter.FreewillFactor}`*\n" +
                $"*Wide context: `{spawnedCharacter.EnableWideContext.ToToggler()}`*\n" +
                $"*Wide context max length: `{spawnedCharacter.WideContextMaxLength}`*\n" +
                $"*Response swipes: `{spawnedCharacter.EnableSwipes.ToToggler()}`*\n" +
                $"*Quotes: `{spawnedCharacter.EnableQuotes.ToToggler()}`*\n" +
                $"*Stop buttons: `{spawnedCharacter.EnableStopButton.ToToggler()}`*\n";

        return info + spawnedCharacter switch
        {
            ISakuraCharacter sakuraCharacter => $"*Chat ID: `{sakuraCharacter.SakuraChatId}`*",
            ICaiCharacter caiCharacter => $"*Chat ID: `{caiCharacter.CaiChatId}`*",

            _ => throw new ArgumentOutOfRangeException(nameof(spawnedCharacter), spawnedCharacter, null)
        };
    }

    #endregion


    #region Formatters

    public static string BringMessageToFormat(string messageFormat, ITextChannel? channel,
                                              (string authorName, string authorMention, string content) message,
                                              (string authorName, string content)? refMessage = null)
    {
        var result = messageFormat.Replace("\\n", "\n")
                                  .Replace(MF_USER, message.authorName)
                                  .Replace(MF_USER_MENTION_HINT, message.authorMention)
                                  .Replace(MF_MSG, message.content)
                                  .Replace(MF_DATETIME, DateTime.Now.ToString("hh:mm dd-MMM-yyyy", new CultureInfo("en-US")));

        if (!messageFormat.Contains(MF_REF_MSG))
        {
            return result;
        }

        var start = messageFormat.IndexOf(MF_REF_BEGIN, StringComparison.Ordinal);
        var end = messageFormat.IndexOf(MF_REF_END, StringComparison.Ordinal) + MF_REF_END.Length;

        if (refMessage is null)
        { // clear format template parts
            return result.Remove(start, end - start).Trim(' ', '\n');
        }

        var refContent = refMessage.Value.content;
        var refAuthor = refMessage.Value.authorName;

        // Replace @mentions with normal names
        var userMentions = USER_MENTION_REGEX.Matches(refContent).ToArray();
        foreach (var mention in userMentions)
        {
            var userId = MentionUtils.ParseUser(mention.Value);
            if (channel!.GetUserAsync(userId).GetAwaiter().GetResult() is not SocketGuildUser mentionedUser)
            {
                continue;
            }

            refContent = refContent.Replace(mention.Value, '@' + mentionedUser.DisplayName);
        }

        // Replace @roles with normal role names
        var roleMentions = ROLE_MENTION_REGEX.Matches(refContent).ToArray();
        foreach (var mention in roleMentions)
        {
            var roleId = MentionUtils.ParseRole(mention.Value);
            var mentionedRole = channel!.Guild.GetRole(roleId);
            if (mentionedRole is null)
            {
                continue;
            }

            refContent = refContent.Replace(mention.Value, '@' + mentionedRole.Name);
        }

        if (refContent.Length > 155)
        {
            refContent = refContent[..150] + "[...]";
        }

        // Fill template
        return result.Replace(MF_REF_USER, refAuthor)
                     .Replace(MF_REF_MSG, refContent)
                     .Replace(MF_REF_BEGIN, string.Empty)
                     .Replace(MF_REF_END, string.Empty)
                     .Replace("\n\n", "\n");
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

    #endregion


    public static string SplitWordsBySep(this string source, char sep)
    {
        var result = new List<char>();
        var chars = source.Replace(" ", "").ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            if (i != 0 && char.IsUpper(chars[i]))
            {
                result.Add(sep);
            }

            result.Add(chars[i]);
        }

        return string.Concat(result);
    }


    public static string ToToggler(this bool bulka)
        => bulka ? "enabled" : "disabled";
}
