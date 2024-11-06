using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models.Abstractions;
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
            try
            {
                await HandleMessageAsync(socketMessage);
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync(e, CommonHelper.NewTraceId());
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

        if (socketUserMessage.Author is not IGuildUser guildUser || guildUser.Id == _discordClient.CurrentUser.Id)
        {
            return;
        }

        var validationResult = WatchDog.ValidateUser(guildUser);
        if (validationResult is not WatchDogValidationResult.Passed)
        {
            return;
        }

        var ensureExistInDbAsync = channel.EnsureExistInDbAsync();

        try
        {
            var taggedCharacter = await FindCharacterByReplyAsync(socketUserMessage) ?? await FindCharacterByPrefixAsync(socketUserMessage);
            if (taggedCharacter is not null)
            {
                await CallCharacterAsync(taggedCharacter, socketUserMessage);
            }

            var randomCharacter = await FindRandomCharacterAsync(socketUserMessage);
            if (randomCharacter is not null && randomCharacter.Id != taggedCharacter?.Id)
            {
                await CallCharacterAsync(randomCharacter, socketUserMessage);
            }
        }
        finally
        {
            await ensureExistInDbAsync;
        }
    }


    private static async Task CallCharacterAsync(ISpawnedCharacter spawnedCharacter, SocketUserMessage socketUserMessage)
    {
        var module = spawnedCharacter.GetIntegrationType().GetIntegrationModule();
        var guildIntegration = await DatabaseHelper.GetGuildIntegrationAsync(spawnedCharacter);
        if (guildIntegration is null)
        {
            return;
        }

        var message = ReformatUserMessage(socketUserMessage, spawnedCharacter);
        var response = await module.CallCharacterAsync(spawnedCharacter, guildIntegration, message);
        var messageId = await spawnedCharacter.SendMessageAsync(response.Content);

        spawnedCharacter.LastCallerDiscordUserId = socketUserMessage.Author.Id;
        spawnedCharacter.LastDiscordMessageId = messageId;
        spawnedCharacter.ResetWithNextMessage = false;
        spawnedCharacter.LastCallTime = DateTime.Now;
        spawnedCharacter.MessagesSent++;

        await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);
    }



    private static string ReformatUserMessage(SocketUserMessage socketUserMessage, ISpawnedCharacter spawnedCharacter)
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
            var refAuthor = (IGuildUser)socketUserMessage.ReferencedMessage.Author;
            var refMsg = socketUserMessage.ReferencedMessage.Content.Trim(' ', '\n').Replace('\n', ' ');

            refMessage = (refAuthor.DisplayName ?? refAuthor.Username, refMsg);
        }

        var messageFormat = spawnedCharacter.MessagesFormat ?? BotConfig.DEFAULT_MESSAGES_FORMAT;
        var author = (IGuildUser)socketUserMessage.Author;
        var channel = (ITextChannel)socketUserMessage.Channel;

        return MH.BringMessageToFormat(messageFormat, channel, (author.DisplayName ?? author.Username, author.Mention, socketUserMessage.Content), refMessage);
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
            var bet = _random.Next(0, 99) + 0.01d;
            if (cachedCharacter.FreewillFactor < bet)
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
