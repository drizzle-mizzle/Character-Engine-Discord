using System.Diagnostics;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.WebSocket;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;

namespace CharacterEngine.App.Handlers;


public class MessagesHandler
{
    private readonly DiscordSocketClient _discordClient;


    public MessagesHandler(DiscordSocketClient discordClient)
    {
        _discordClient = discordClient;
    }


    public Task HandleMessage(SocketMessage socketMessage)
    {
        Task.Run(async () =>
        {
            var traceId = CommonHelper.NewTraceId();
            try
            {
                await HandleMessageAsync(socketMessage, traceId);
            }
            catch (Exception e)
            {
                var channel = socketMessage.Channel as IGuildChannel;

                var guild = channel?.Guild;
                if (guild is null)
                {
                    return;
                }

                var owner = await guild.GetOwnerAsync();
                var content = $"TraceID: **{traceId}**\n" +
                              $"User: **{socketMessage.Author.GlobalName ?? socketMessage.Author.Username}** ({socketMessage.Author.Id})\n" +
                              $"Channel: **{channel!.Name}** ({channel.Id})\n" +
                              $"Guild: **{guild.Name}** ({guild.Id})\n" +
                              $"Owned by: **{owner?.DisplayName ?? owner?.Username}** ({owner?.Id})\n\n" +
                              $"Exception:\n{e}";

                await _discordClient.ReportErrorAsync("MessagesHandler exception", content, traceId, writeMetric: false);
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleMessageAsync(SocketMessage socketMessage, string traceId)
    {
        if (socketMessage.Channel is not ITextChannel channel)
        {
            return;
        }

        if (socketMessage is not SocketUserMessage socketUserMessage)
        {
            return;
        }

        if (socketUserMessage.Author is not IGuildUser guildUser || guildUser.Id == _discordClient.CurrentUser.Id)
        {
            return;
        }

        var validationResult = WatchDog.ValidateUser(guildUser);
        if (validationResult is not WatchDogValidationResult.Passed)
        {
            return;
        }

        if (socketUserMessage.Content.StartsWith("~ignore", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var tasks = new List<Task>();

            var taggedCharacter = await FindCharacterByReplyAsync(socketUserMessage) ?? await FindCharacterByPrefixAsync(socketUserMessage);
            if (taggedCharacter is not null)
            {
                tasks.Add(CallCharacterAsync(taggedCharacter, socketUserMessage, false));
            }

            var randomCharacter = await FindRandomCharacterAsync(socketUserMessage);
            if (randomCharacter is not null && randomCharacter.Id != taggedCharacter?.Id)
            {
                tasks.Add(CallCharacterAsync(randomCharacter, socketUserMessage, true));
            }

            Task.WaitAll(tasks.ToArray());
        }
        catch (Exception e)
        {
            var userFriendlyExceptionCheck = e.IsUserFriendlyException();
            if (userFriendlyExceptionCheck.valid)
            {
                var message = $"{MessagesTemplates.WARN_SIGN_DISCORD} Failed to fetch character response: {userFriendlyExceptionCheck.message}";
                await socketUserMessage.ReplyAsync(embed: message.ToInlineEmbed(Color.Orange));
            }
        }
    }


    private static async Task CallCharacterAsync(ISpawnedCharacter spawnedCharacter, SocketUserMessage socketUserMessage, bool randomCall)
    {
        var channel = (ITextChannel)socketUserMessage.Channel;
        await channel.EnsureExistInDbAsync();

        if (spawnedCharacter.IsNfsw && !channel.IsNsfw)
        {
            var nsfwMsg = $"**{spawnedCharacter.CharacterName}** is NSFW character and can be called only in channels with age restriction.";
            await socketUserMessage.ReplyAsync(embed: nsfwMsg.ToInlineEmbed(Color.Purple));
            return;
        }

        var module = spawnedCharacter.GetIntegrationType().GetIntegrationModule();
        var guildIntegration = await DatabaseHelper.GetGuildIntegrationAsync(spawnedCharacter);
        if (guildIntegration is null)
        {
            return;
        }

        var author = socketUserMessage.Author;
        var cachedCharacter = MemoryStorage.CachedCharacters.Find(spawnedCharacter.Id)!;
        if (cachedCharacter.QueueIsFull || cachedCharacter.QueueContains(author.Id))
        {
            return;
        }

        cachedCharacter.QueueAdd(author.Id);
        try
        {
            // Wait for the first place in queue
            var sw = Stopwatch.StartNew();
            while (!cachedCharacter.QueueIsTurnOf(author.Id))
            {
                // just in case it hang on for some reason (are there any?)
                if (sw.Elapsed.TotalMinutes >= 2d)
                {
                    return;
                }

                await Task.Delay(500);
            }

            // Wait for delay
            var responseDelay = author.IsBot || author.IsWebhook ? Math.Max(5, spawnedCharacter.ResponseDelay) : spawnedCharacter.ResponseDelay;
            if (responseDelay > 0)
            {
                await Task.Delay((int)(responseDelay * 1000));
            }

            string userMessage;
            if (randomCall && spawnedCharacter.FreewillContextSize != 0)
            {
                userMessage = "";
                var messageLength = 0;
                var downloadedMessages = (await channel.GetMessagesAsync(20).FlattenAsync()).ToList();

                foreach (var downloadedMessage in downloadedMessages.Select(m => m as IUserMessage))
                {
                    if (downloadedMessage is null
                     || downloadedMessage.Id <= cachedCharacter.WideContextLastMessageId
                     || downloadedMessage.Author.Id == CharacterEngineBot.DiscordShardedClient.CurrentUser.Id
                     || downloadedMessage.Author.Id == spawnedCharacter.WebhookId
                     || downloadedMessage.Content.Trim('\n', ' ').Length == 0)
                    {
                        continue;
                    }

                    var messagePartial = ReformatUserMessage(downloadedMessage, spawnedCharacter) + "\n\n";
                    messageLength += messagePartial.Length;

                    if (messageLength <= spawnedCharacter.FreewillContextSize)
                    {
                        userMessage = messagePartial + userMessage;
                        continue;
                    }

                    break;
                }

                cachedCharacter.WideContextLastMessageId = downloadedMessages.FirstOrDefault()?.Id; // remember
            }
            else
            {
                userMessage = ReformatUserMessage(socketUserMessage, spawnedCharacter);
                cachedCharacter.WideContextLastMessageId = socketUserMessage.Id;
            }

            var response = await module.CallCharacterAsync(spawnedCharacter, guildIntegration, userMessage);
            var responseMessage = randomCall ? response.Content : $"{socketUserMessage.Author.Mention} {response.Content}";
            var messageId = await spawnedCharacter.SendMessageAsync(responseMessage);

            spawnedCharacter.LastCallerDiscordUserId = socketUserMessage.Author.Id;
            spawnedCharacter.LastDiscordMessageId = messageId;
            spawnedCharacter.ResetWithNextMessage = false;
            spawnedCharacter.LastCallTime = DateTime.Now;
            spawnedCharacter.MessagesSent++;
            await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

            MetricsWriter.Create(MetricType.CharacterCalled, spawnedCharacter.Id, $"{channel.Id}:{channel.Guild.Id}", silent: true);
        }
        finally
        {
            cachedCharacter.QueueRemove(author.Id);
        }
    }



    private static string ReformatUserMessage(IUserMessage socketUserMessage, ISpawnedCharacter spawnedCharacter)
    {
        var message = socketUserMessage.Content.Trim(' ', '\n');

        if (message.StartsWith(spawnedCharacter.CallPrefix, StringComparison.Ordinal))
        {
            message = message[spawnedCharacter.CallPrefix.Length..].Trim();
        }

        while (message.Contains("\n\n\n"))
        {
            message = message.Replace("\n\n\n", "\n\n");
        }

        (string, string)? refMessage = null;
        if (socketUserMessage.ReferencedMessage is not null && !string.IsNullOrWhiteSpace(socketUserMessage.ReferencedMessage.Content))
        {
            var refAuthor = socketUserMessage.ReferencedMessage.Author;
            var refMsg = socketUserMessage.ReferencedMessage.Content.Trim(' ', '\n').Replace('\n', ' ');

            var refAuthorName = refAuthor is IGuildUser gu ? gu.DisplayName ?? gu.Username : refAuthor.GlobalName ?? refAuthor.Username;
            refMessage = (refAuthorName, refMsg);
        }

        var messageFormat = spawnedCharacter.MessagesFormat ?? BotConfig.DEFAULT_MESSAGES_FORMAT;
        var author = socketUserMessage.Author;
        var channel = (ITextChannel)socketUserMessage.Channel;

        var authorName = author is IGuildUser gAuthor ? gAuthor.DisplayName ?? gAuthor.Username : author.GlobalName ?? author.Username;

        return MH.BringMessageToFormat(messageFormat, channel, (authorName, author.Mention, message), refMessage);
    }


    private static Task<ISpawnedCharacter?> FindCharacterByReplyAsync(SocketUserMessage socketUserMessage)
    {
        if (socketUserMessage.ReferencedMessage?.Author?.Id is not ulong webhookId)
        {
            return Task.FromResult<ISpawnedCharacter?>(null);
        }

        var cachedCharacter = MemoryStorage.CachedCharacters.Find(webhookId.ToString(), socketUserMessage.Channel.Id);
        if (cachedCharacter is null)
        {
            return Task.FromResult<ISpawnedCharacter?>(null);
        }

        return DatabaseHelper.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
    }


    private static Task<ISpawnedCharacter?> FindCharacterByPrefixAsync(SocketUserMessage socketUserMessage)
    {
        var cachedCharacters = MemoryStorage.CachedCharacters.ToList(socketUserMessage.Channel.Id);
        if (cachedCharacters.Count == 0)
        {
            return Task.FromResult<ISpawnedCharacter?>(null);
        }

        var content = socketUserMessage.Content.Trim(' ', '\n');

        var cachedCharacter = cachedCharacters.FirstOrDefault(c => c.WebhookId != socketUserMessage.Author.Id.ToString() && content.StartsWith(c.CallPrefix, StringComparison.Ordinal));
        if (cachedCharacter is null)
        {
            return Task.FromResult<ISpawnedCharacter?>(null);
        }

        return DatabaseHelper.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
    }


    private static readonly Random _random = new();
    private static async Task<ISpawnedCharacter?> FindRandomCharacterAsync(SocketUserMessage socketUserMessage)
    {
        var cachedCharacters = MemoryStorage.CachedCharacters.ToList(socketUserMessage.Channel.Id).Where(c => c.FreewillFactor > 0 && c.WebhookId != socketUserMessage.Author.Id.ToString()).ToList();
        if (cachedCharacters.Count == 0)
        {
            return null;
        }

        var spawnedCharacters = new List<ISpawnedCharacter>();
        foreach (var cachedCharacter in cachedCharacters)
        {
            var bet = _random.NextDouble() + 0.01d;
            if ((cachedCharacter.FreewillFactor / 100) < bet)
            {
                continue;
            }

            var spawnedCharacter = await DatabaseHelper.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
            if (spawnedCharacter is not null)
            {
                spawnedCharacters.Add(spawnedCharacter);
            }
        }

        if (spawnedCharacters.Count == 0)
        {
            return null;
        }

        // Try to find mentioned character
        foreach (var spawnedCharacter in spawnedCharacters)
        {
            var characterNameWords = spawnedCharacter.CharacterName.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (characterNameWords.Any(word => socketUserMessage.Content.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                return spawnedCharacter;
            }

            if (socketUserMessage.Content.Contains(spawnedCharacter.CallPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return spawnedCharacter;
            }
        }

        return spawnedCharacters.OrderBy(c => c.MessagesSent).First(); // return less active one
    }
}
