using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.Helpers.Common;
using CharacterEngineDiscord.Helpers.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using Discord.WebSocket;
using NLog;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;

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


        var validationResult = await WatchDog.ValidateAsync(socketUserMessage.Author.Id, channel.GuildId);
        if (validationResult is not WatchDogValidationResult.Passed)
        {
            return;
        }

        _ = channel.EnsureExistInDbAsync();
        CachedCharacterInfo? cachedCharacter;
        if (socketUserMessage.ReferencedMessage?.Author?.Id is ulong rmUserId)
        {
            cachedCharacter = MemoryStorage.CachedCharacters.GetByWebhookId(rmUserId);
        }
        else
        {
            var cachedCharacters = MemoryStorage.CachedCharacters.ToList().Where(c => c.ChannelId == channel.Id);
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
}
