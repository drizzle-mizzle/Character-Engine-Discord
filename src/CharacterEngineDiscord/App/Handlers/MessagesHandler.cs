using System.Diagnostics;
using CharacterAi.Client.Exceptions;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Decorators;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Infrastructure;
using CharacterEngine.App.Repositories;
using CharacterEngine.App.Repositories.Storages;
using CharacterEngine.App.Services;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Shared.Helpers;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;

namespace CharacterEngine.App.Handlers;


public class MessagesHandler
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;
    private readonly IntegrationsRepository _integrationsRepository;
    private readonly CharactersRepository _charactersRepository;
    private readonly CacheRepository _cacheRepository;


    public MessagesHandler(
        AppDbContext db,
        DiscordSocketClient discordClient,
        IntegrationsRepository integrationsRepository,
        CharactersRepository charactersRepository,
        CacheRepository cacheRepository
    )
    {
        _db = db;
        _discordClient = discordClient;
        _integrationsRepository = integrationsRepository;
        _charactersRepository = charactersRepository;
        _cacheRepository = cacheRepository;
    }


    public Task HandleMessage(SocketMessage socketMessage)
    {
        Task.Run(async () =>
        {
            try
            {
                await HandleMessageAsync(socketMessage);
            }
            catch (Discord.Net.HttpException)
            {
                // ignore
            }
            catch (Exception e)
            {
                if (e is UnauthorizedAccessException or UserFriendlyException)
                {
                    return;
                }

                var channel = socketMessage.Channel as IGuildChannel;

                var guild = channel?.Guild;
                if (guild is null)
                {
                    return;
                }

                var traceId = CommonHelper.NewTraceId();
                var owner = await guild.GetOwnerAsync();
                var userName = socketMessage.Author.GlobalName ?? socketMessage.Author.Username;

                var title = $"📧MessagesHandler Exception [{userName}]";

                var header = $"TraceID: **{traceId}**\n" +
                             $"User: **{userName}** ({socketMessage.Author.Id})\n" +
                             $"Channel: **{channel!.Name}** ({channel.Id})\n" +
                             $"Guild: **{guild.Name}** ({guild.Id})\n" +
                             $"Owned by: **{owner?.Username}** ({owner?.Id})";

                await _discordClient.ReportErrorAsync(title, header, e, traceId, writeMetric: false);
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleMessageAsync(SocketMessage socketMessage)
    {
        if (socketMessage is not SocketUserMessage socketUserMessage)
        {
            return;
        }

        if (socketUserMessage.Author is not IGuildUser guildUser || guildUser.Id == _discordClient.CurrentUser.Id)
        {
            return;
        }

        if (socketUserMessage.Content.StartsWith("~ignore", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (socketUserMessage.Channel is not ITextChannel textChannel)
        {
            throw new UserFriendlyException("Bot can operate only in text channels");
        }

        _cacheRepository.EnsureChannelCached(textChannel);

        var validation = WatchDog.ValidateUser(guildUser, null, justCheck: true);
        if (validation.Result is not WatchDogValidationResult.Passed)
        {
            return;
        }

        try
        {
            var primaryChannelId = textChannel is SocketThreadChannel threadChannel ? threadChannel.ParentChannel.Id : textChannel.Id;
            var callTasks = new List<Task>();

            var taggedCharacter = await FindCharacterByReplyAsync(socketUserMessage, primaryChannelId) ?? await FindCharacterByPrefixAsync(socketUserMessage, primaryChannelId);

            if (taggedCharacter is not null)
            {
                callTasks.Add(CallCharacterAsync(taggedCharacter, socketUserMessage, false));
            }

            var cachedCharacters = _cacheRepository.CachedCharacters
                                                   .ToList(primaryChannelId)
                                                   .Where(c => c.FreewillFactor > 0 && c.WebhookId != socketUserMessage.Author.Id.ToString())
                                                   .ToList();

            var randomCharacter = await FindRandomCharacterAsync(socketUserMessage, cachedCharacters);

            if (randomCharacter is not null && randomCharacter.Id != taggedCharacter?.Id)
            {
                callTasks.Add(CallCharacterAsync(randomCharacter, socketUserMessage, isIndirectCall: true));
            }

            var hunterCharacters = await FindHunterCharactersAsync(socketUserMessage, cachedCharacters);

            if (hunterCharacters.Count != 0)
            {
                var callCharactersByHuntedUsersAsync = hunterCharacters.Select(hc => CallCharacterAsync(hc, socketUserMessage, isIndirectCall: false));
                callTasks.AddRange(callCharactersByHuntedUsersAsync);
            }

            if (callTasks.Count != 0 && !(guildUser.IsBot || guildUser.IsWebhook))
            {
                _cacheRepository.EnsureUserCached(guildUser);
                MetricsWriter.Write(MetricType.NewInteraction, guildUser.Id, $"{MetricUserSource.CharacterCall:G}:{textChannel.Id}:{textChannel.GuildId}", true);
            }

            Task.WaitAll(callTasks.ToArray());
        }
        catch (Exception e)
        {
            if (e is CharacterAiException characterAiException)
            {
                _ = _discordClient.ReportErrorAsync($"{BotConfig.CHARACTER_AI_EMOJI} C.AI Exception", characterAiException.Message, characterAiException, "", true);
            }

            var userFriendlyExceptionCheck = e.ValidateUserFriendlyException();
            if (userFriendlyExceptionCheck.Pass)
            {
                var embed = new EmbedBuilder().WithColor(Color.Orange)
                                              .WithTitle($"{MessagesTemplates.WARN_SIGN_DISCORD} Failed to fetch character response")
                                              .WithDescription($"Details:\n```\n{userFriendlyExceptionCheck.Message}\n```")
                                              .WithFooter($"ERROR TRACE ID: {CommonHelper.NewTraceId()}");

                await socketUserMessage.ReplyAsync(embed: embed.Build());
            }
            else
            {
                throw;
            }
        }
    }


    private async Task CallCharacterAsync(ISpawnedCharacter spawnedCharacter, SocketUserMessage socketUserMessage, bool isIndirectCall)
    {
        var channel = (ITextChannel)socketUserMessage.Channel;

        if (spawnedCharacter.IsNfsw && !channel.IsNsfw)
        {
            var nsfwMsg = $"{spawnedCharacter.GetMention()} is NSFW character and can be called only in channels with age restriction.";
            await socketUserMessage.ReplyAsync(embed: nsfwMsg.ToInlineEmbed(Color.Purple));
            return;
        }

        var guildIntegration = await _integrationsRepository.GetGuildIntegrationAsync(spawnedCharacter);
        if (guildIntegration is null)
        {
            return;
        }

        var author = socketUserMessage.Author;
        var cachedCharacter = _cacheRepository.CachedCharacters.Find(spawnedCharacter.Id)!;
        if (cachedCharacter.QueueIsFullFor(author.Id))
        {
            return;
        }

        cachedCharacter.QueueAddCaller(author.Id);
        try
        {
            // Wait for the first place in queue
            var sw = Stopwatch.StartNew();
            while (!cachedCharacter.QueueIsTurnOf(author.Id))
            {
                // just in case it hang on for some reason (are there any?)
                if (sw.Elapsed.TotalMinutes >= 2)
                {
                    return;
                }

                await Task.Delay(500);
            }

            spawnedCharacter = (await _charactersRepository.GetSpawnedCharacterByIdAsync(spawnedCharacter.Id))!; // force data reload
            if (spawnedCharacter is null)
            {
                return;
            }

            // Wait for delay
            var responseDelay = author.IsBot || author.IsWebhook ? Math.Max(5, spawnedCharacter.ResponseDelay) : spawnedCharacter.ResponseDelay;
            if (responseDelay > 0)
            {
                await Task.Delay((int)(responseDelay * 1000));
            }

            var messageFormat = spawnedCharacter.MessagesFormat;
            if (messageFormat is null)
            {
                var formats = await _db.DiscordChannels
                                      .Include(c => c.DiscordGuild)
                                      .Where(c => c.Id == spawnedCharacter.DiscordChannelId)
                                      .Select(c => new
                                       {
                                           ChannelMessagesFormat = c.MessagesFormat,
                                           GuildMessagesFormat = c.DiscordGuild.MessagesFormat
                                       })
                                      .FirstAsync();
                messageFormat = formats.ChannelMessagesFormat ?? formats.GuildMessagesFormat ?? BotConfig.DEFAULT_MESSAGES_FORMAT;
            }

            string userMessage;
            if (isIndirectCall && spawnedCharacter.FreewillContextSize != 0)
            {
                userMessage = string.Empty;
                var messageLength = 0;
                var downloadedMessages = (await channel.GetMessagesAsync(20).FlattenAsync()).ToList();

                foreach (var downloadedMessage in downloadedMessages.Select(m => m as IUserMessage))
                {
                    if (downloadedMessage is null
                     || downloadedMessage.Id <= cachedCharacter.WideContextLastMessageId
                     || downloadedMessage.Author.Id == CharacterEngineBot.DiscordClient.CurrentUser.Id
                     || downloadedMessage.Author.Id == spawnedCharacter.WebhookId
                     || downloadedMessage.Content.Trim('\n', ' ').Length == 0)
                    {
                        continue;
                    }

                    var messagePartial = ReformatUserMessage(downloadedMessage, spawnedCharacter.CallPrefix, messageFormat) + "\n\n";
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
                userMessage = ReformatUserMessage(socketUserMessage, spawnedCharacter.CallPrefix, messageFormat);
                cachedCharacter.WideContextLastMessageId = socketUserMessage.Id;
            }

            var integrationType = spawnedCharacter.GetIntegrationType();
            var response = await IntegrationsHub.GetChatModule(integrationType)
                                                .CallCharacterAsync(spawnedCharacter, guildIntegration, userMessage);

            var responseMessage = isIndirectCall ? response.Content : $"{socketUserMessage.Author.Mention} {response.Content}";
            ulong messageId;

            try
            {
                var webhook = _cacheRepository.CachedWebhookClients.FindOrCreate(spawnedCharacter.WebhookId, spawnedCharacter.WebhookToken);
                var activeCharacter = new ActiveCharacterDecorator(spawnedCharacter, webhook);
                ulong? threadId = socketUserMessage.Channel is SocketThreadChannel threadChannel ? threadChannel.Id : null;

                messageId = await activeCharacter.SendMessageAsync(responseMessage, socketUserMessage.Author.Mention, threadId);
            }
            catch (Exception e)
            {
                var webhookExceptionCheck = e.ValidateWebhookException();
                if (webhookExceptionCheck.Pass)
                {
                    _cacheRepository.CachedWebhookClients.Remove(spawnedCharacter.WebhookId);
                    _cacheRepository.CachedCharacters.Remove(spawnedCharacter.Id);

                    await _charactersRepository.DeleteSpawnedCharacterAsync(spawnedCharacter.Id);
                }

                throw;
            }

            spawnedCharacter.LastCallerDiscordUserId = socketUserMessage.Author.Id;
            spawnedCharacter.LastDiscordMessageId = messageId;
            spawnedCharacter.LastCallTime = DateTime.Now;
            spawnedCharacter.MessagesSent++;
            await _charactersRepository.UpdateSpawnedCharacterAsync(spawnedCharacter);

            MetricsWriter.Write(MetricType.CharacterCalled, spawnedCharacter.Id, $"{channel.Id}:{channel.Guild.Id}", silent: true);
        }
        finally
        {
            cachedCharacter.QueueRemove(author.Id);
        }
    }



    private static string ReformatUserMessage(IUserMessage socketUserMessage, string characterCallPrefix, string messageFormat)
    {
        var message = socketUserMessage.Content.Trim(' ', '\n');

        if (message.StartsWith(characterCallPrefix, StringComparison.Ordinal))
        {
            message = message[characterCallPrefix.Length..].Trim();
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

        var channel = (ITextChannel)socketUserMessage.Channel;
        var author = socketUserMessage.Author;
        var authorName = author is IGuildUser gAuthor ? gAuthor.DisplayName ?? gAuthor.Username : author.GlobalName ?? author.Username;

        return MH.BringMessageToFormat(messageFormat, channel, (authorName, author.Mention, message), refMessage);
    }


    private Task<ISpawnedCharacter?> FindCharacterByReplyAsync(SocketUserMessage socketUserMessage, ulong channelId)
    {
        if (socketUserMessage.ReferencedMessage?.Author?.Id is not ulong webhookId)
        {
            return Task.FromResult<ISpawnedCharacter?>(null);
        }

        var cachedCharacter = _cacheRepository.CachedCharacters.Find(webhookId.ToString(), channelId);
        if (cachedCharacter is null)
        {
            return Task.FromResult<ISpawnedCharacter?>(null);
        }

        return _charactersRepository.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
    }


    private Task<ISpawnedCharacter?> FindCharacterByPrefixAsync(SocketUserMessage socketUserMessage, ulong channelId)
    {
        var cachedCharacters = _cacheRepository.CachedCharacters.ToList(channelId);
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

        return _charactersRepository.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
    }


    private static readonly Random _random = new();
    private async Task<ISpawnedCharacter?> FindRandomCharacterAsync(SocketUserMessage socketUserMessage, List<CachedCharacterInfo> cachedCharacters)
    {
        if (cachedCharacters.Count == 0)
        {
            return null;
        }

        var randomlyCalledCharacters = new List<ISpawnedCharacter>();
        foreach (var cachedCharacter in cachedCharacters)
        {
            var bet = _random.NextDouble() + 0.01d; // 0.01 - 1.00
            var claim = cachedCharacter.FreewillFactor / 100; // 0 - 1.00
            
            // 1.00 - claim = chance that character will NOT be called
            if (bet <= claim)
            {
                var spawnedCharacter = await _charactersRepository.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
                randomlyCalledCharacters.Add(spawnedCharacter!);
            }
        }

        switch (randomlyCalledCharacters.Count)
        {
            case 0: return null;
            case 1: return randomlyCalledCharacters[0];
        }

        // Try to find mentioned character
        foreach (var spawnedCharacter in randomlyCalledCharacters)
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

        return randomlyCalledCharacters.OrderBy(c => c.MessagesSent).First(); // return less active one
    }


    private async Task<List<ISpawnedCharacter>> FindHunterCharactersAsync(SocketUserMessage socketUserMessage, List<CachedCharacterInfo> cachedCharacters)
    {
        if (cachedCharacters.Count == 0)
        {
            return [];
        }

        var spawnedCharacters = new List<ISpawnedCharacter>();
        foreach (var cachedCharacter in cachedCharacters)
        {
            if (cachedCharacter.HuntedUsers.Contains(socketUserMessage.Author.Id))
            {
                var spawnedCharacter = await _charactersRepository.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
                spawnedCharacters.Add(spawnedCharacter!);
            }
        }

        return spawnedCharacters;
    }
}
