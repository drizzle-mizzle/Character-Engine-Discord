using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using Discord.WebSocket;
using NLog;
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


    private static async Task HandleMessageAsync(SocketMessage socketMessage)
    {
        if (socketMessage.Channel is not ITextChannel channel)
        {
            return;
        }

        if (socketMessage is not SocketUserMessage socketUserMessage)
        {
            return;
        }

        if (socketUserMessage.Author is not IGuildUser guildUser)
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
            var cachedCharacter = FindCharacterByReply(socketUserMessage) ?? FindCharacterByPrefix(socketUserMessage);
            if (cachedCharacter is null)
            {
                return;
            }

            var spawnedCharacter = await DatabaseHelper.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
            if (spawnedCharacter is null)
            {
                return;
            }

            await CallCharacterAsync(spawnedCharacter, socketUserMessage);

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


    private static CachedCharacterInfo? FindCharacterByReply(SocketUserMessage socketUserMessage)
    {
        if (socketUserMessage.ReferencedMessage?.Author?.Id is not ulong webhookId)
        {
            return null;
        }

        return MemoryStorage.CachedCharacters.Find(webhookId.ToString(), socketUserMessage.Channel.Id);
    }


    private static CachedCharacterInfo? FindCharacterByPrefix(SocketUserMessage socketUserMessage)
    {
        var cachedCharacters = MemoryStorage.CachedCharacters.ToList(socketUserMessage.Channel.Id);
        if (cachedCharacters.Count == 0)
        {
            return null;
        }

        var content = socketUserMessage.Content.Trim(' ', '\n');
        return cachedCharacters.FirstOrDefault(c => content.StartsWith(c.CallPrefix, StringComparison.Ordinal));
    }
}
