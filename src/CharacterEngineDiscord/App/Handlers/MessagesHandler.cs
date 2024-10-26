using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Helpers.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using Discord.WebSocket;
using NLog;
using WebSocketSharp;

namespace CharacterEngine.App.Handlers;


public class MessagesHandler
{
    private readonly ILogger _log;
    private AppDbContext _db { get; set; }

    private readonly DiscordSocketClient _discordClient;


    public MessagesHandler(ILogger log, AppDbContext db, DiscordSocketClient discordClient)
    {
        _log = log;
        _db = db;

        _discordClient = discordClient;
    }


    public Task HandleMessage(SocketMessage socketMessage)
    {
        Task.Run(async () =>
        {
            try
            {
                await HandleMessageAsync(socketMessage);
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync(e);
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleMessageAsync(SocketMessage socketMessage)
    {
        if (socketMessage.Channel is not ITextChannel channel)
        {
            return;
        }


        if (socketMessage is not SocketUserMessage socketUserMessage)
        {
            return;
        }

        _ = channel.EnsureExistInDbAsync();

        CachedCharacterInfo? cachedCharacter;
        if (socketUserMessage.ReferencedMessage?.Author?.Id is ulong rmUserId)
        {
            cachedCharacter = StaticStorage.CachedCharacters.GetByWebhookId(rmUserId);
        }
        else
        {
            var cachedCharacters = StaticStorage.CachedCharacters.ToList().Where(c => c.ChannelId == channel.Id);
            var content = socketUserMessage.Content.Trim(' ', '\n');
            cachedCharacter = cachedCharacters.FirstOrDefault(c => content.StartsWith(c.CallPrefix, StringComparison.Ordinal));
        }

        if (cachedCharacter is null)
        {
            return;
        }

        await CallCharacterAsync(cachedCharacter, socketUserMessage);
    }


    private static async Task CallCharacterAsync(CachedCharacterInfo cachedCharacter, SocketUserMessage socketUserMessage)
    {
        var spawnedCharacter = await DatabaseHelper.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
        if (spawnedCharacter is null)
        {
            return;
        }

        var module = spawnedCharacter.GetIntegrationType().GetIntegrationModule();
        var guildIntegration = await DatabaseHelper.GetGuildIntegrationAsync(spawnedCharacter);

        var message = ReformatUserMessage(socketUserMessage, spawnedCharacter);
        var response = await module.CallCharacterAsync(spawnedCharacter, guildIntegration, message);

        if (spawnedCharacter.ResetWithNextMessage)
        {
            spawnedCharacter.ResetWithNextMessage = false;
            await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);
        }

        await spawnedCharacter.SendMessageAsync(response.Content);
    }



    private static string ReformatUserMessage(SocketUserMessage socketUserMessage, ISpawnedCharacter spawnedCharacter)
    {
        var messageFormat = spawnedCharacter.MessagesFormat;
        var message = socketUserMessage.Content.Trim(' ', '\n');

        if (message.StartsWith(spawnedCharacter.CallPrefix, StringComparison.Ordinal))
        {
            message = message[spawnedCharacter.CallPrefix.Length..].Trim();
        }

        var author = (SocketGuildUser)socketUserMessage.Author;
        message = messageFormat.Replace(MessagesHelper.TP_USER, author.DisplayName)
                               .Replace(MessagesHelper.TP_USER_MENTION_HINT, author.Mention)
                               .Replace(MessagesHelper.TP_MSG, message)
                               .Replace(MessagesHelper.TP_DATETIME, DateTime.Now.ToString("hh:mm dd-MMM-yyyy"));

        while (message.Contains("\n\n\n"))
        {
            message = message.Replace("\n\n\n", "\n\n");
        }

        if (!messageFormat.Contains(MessagesHelper.TP_REF_MSG))
        {
            return message;
        }

        var start = messageFormat.IndexOf(MessagesHelper.TP_REF_BEGIN, StringComparison.Ordinal);
        var end = messageFormat.IndexOf(MessagesHelper.TP_REF_END, StringComparison.Ordinal) + MessagesHelper.TP_REF_END.Length;

        var refMsg = socketUserMessage.ReferencedMessage?.Content.Trim(' ', '\n').Replace('\n', ' ');
        if (string.IsNullOrWhiteSpace(refMsg))
        { // clear format template parts
            return message.Remove(start, end - start).Trim(' ', '\n');
        }

        // Replace @mentions with normal names
        var userMentions = MessagesHelper.USER_MENTION_REGEX.Matches(refMsg).ToArray();
        foreach (var mention in userMentions)
        {
            var userId = MentionUtils.ParseUser(mention.Value);
            var mentionedUser = socketUserMessage.Channel.GetUserAsync(userId).GetAwaiter().GetResult() as SocketGuildUser;
            if (mentionedUser is null)
            {
                continue;
            }

            refMsg = refMsg.Replace(mention.Value, '@' + mentionedUser.DisplayName);
        }

        // Replace @roles with normal role names
        var roleMentions = MessagesHelper.ROLE_MENTION_REGEX.Matches(refMsg).ToArray();
        foreach (var mention in roleMentions)
        {
            var roleId = MentionUtils.ParseRole(mention.Value);
            var mentionedRole = ((SocketTextChannel)socketUserMessage.Channel).Guild.GetRole(roleId);;
            if (mentionedRole is null)
            {
                continue;
            }

            refMsg = refMsg.Replace(mention.Value, '@' + mentionedRole.Name);
        }

        // Fill template
        var refAuthor = (SocketGuildUser)socketUserMessage.ReferencedMessage!.Author;
        message = message.Replace(MessagesHelper.TP_REF_USER, refAuthor.DisplayName)
                         .Replace(MessagesHelper.TP_REF_MSG, refMsg.Length > 155 ? refMsg[..150] + "[...]" : refMsg)
                         .Replace(MessagesHelper.TP_REF_BEGIN, string.Empty)
                         .Replace(MessagesHelper.TP_REF_END, string.Empty);

        return message;
    }
}
