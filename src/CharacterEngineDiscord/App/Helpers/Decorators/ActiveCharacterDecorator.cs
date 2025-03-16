using CharacterEngineDiscord.Domain.Models.Abstractions;
using Discord;
using Discord.Webhook;

namespace CharacterEngine.App.Helpers;


public class ActiveCharacterDecorator
{
    private readonly ISpawnedCharacter _spawnedCharacter;
    private readonly DiscordWebhookClient _discordWebhookClient;


    public ActiveCharacterDecorator(ISpawnedCharacter spawnedCharacter, DiscordWebhookClient discordWebhookClient)
    {
        _spawnedCharacter = spawnedCharacter;
        _discordWebhookClient = discordWebhookClient;
    }


    public async Task<ulong> SendGreetingAsync(string username, ulong? threadId = null)
    {
        if (string.IsNullOrWhiteSpace(_spawnedCharacter.CharacterFirstMessage))
        {
            return 0;
        }

        var characterMessage = _spawnedCharacter.CharacterFirstMessage
                                                .Replace("{{char}}", _spawnedCharacter.CharacterName)
                                                .Replace("{{user}}", $"**{username}**");

        return await SendMessageAsync(characterMessage, threadId);
    }


    public async Task<ulong> SendMessageAsync(string characterMessage, ulong? threadId = null)
    {
        if (characterMessage.Length <= 2000)
        {
            return await (threadId is null ? _discordWebhookClient.SendMessageAsync(characterMessage) : _discordWebhookClient.SendMessageAsync(characterMessage, threadId: threadId));
        }

        var chunkSize = characterMessage.Length > 3990 ? 1990 : characterMessage.Length / 2;
        var chunks = characterMessage.Chunk(chunkSize).Select(c => new string(c)).ToArray();

        ulong messageId;
        if (threadId is null)
        {
            messageId = await _discordWebhookClient.SendMessageAsync(chunks[0]);
            var channel = (ITextChannel)CharacterEngineBot.DiscordClient.GetChannel(threadId ?? _spawnedCharacter.DiscordChannelId);
            var message = await channel.GetMessageAsync(messageId);
            var thread = await channel.CreateThreadAsync("[MESSAGE LENGTH LIMIT EXCEEDED]", message: message);

            for (var i = 1; i < chunks.Length; i++)
            {
                await _discordWebhookClient.SendMessageAsync(chunks[i], threadId: thread.Id);
            }

            await thread.ModifyAsync(t => { t.Archived = true; });
        }
        else
        {
            messageId = await _discordWebhookClient.SendMessageAsync(chunks[0] + "[...]", threadId: threadId);
        }

        return messageId;

    }

}
