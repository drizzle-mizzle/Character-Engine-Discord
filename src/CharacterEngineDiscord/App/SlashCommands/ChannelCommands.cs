using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using MP = CharacterEngine.App.Helpers.Discord.MessagesTemplates;

namespace CharacterEngine.App.SlashCommands;


[ValidateChannelPermissions]
[ValidateAccessLevel(AccessLevels.Manager)]
[Group("channel", "Configure per-channel settings")]
public class ChannelCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;


    public ChannelCommands(AppDbContext db)
    {
        _db = db;
    }


    [SlashCommand("messages-format", "Messages format")]
    public async Task MessagesFormat(MessagesFormatAction action, string? newFormat = null)
    {
        await DeferAsync();

        var message = await InteractionsHelper.SharedMessagesFormatAsync(MessagesFormatTarget.channel, action, Context.Channel.Id, newFormat);

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    [SlashCommand("no-warn", "Disable/enable permissions warning")]
    public async Task NoWarn(bool toggle)
    {
        await DeferAsync();

        var channel = await _db.DiscordChannels.FirstAsync(c => c.Id == Context.Channel.Id);
        channel.NoWarn = toggle;
        await _db.SaveChangesAsync();

        var message = $"{MP.OK_SIGN_DISCORD} Permission validations for the current channel were {(toggle ? "disabled" : "enabled")}";
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Orange));
    }


    [SlashCommand("list-characters", "Show a list of all characters spawned in the current channel")]
    public async Task ListCharacters()
    {
        await RespondAsync(embed: MP.WAIT_MESSAGE);

        var spawnedCharacters = await DatabaseHelper.GetAllSpawnedCharactersInChannelAsync(Context.Channel.Id);
        if (spawnedCharacters.Count == 0)
        {
            await FollowupAsync(embed: "This channel has no spawned characters".ToInlineEmbed(Color.Magenta));
            return;
        }

        var listEmbed = MessagesHelper.BuildCharactersList(spawnedCharacters.OrderByDescending(c => c.MessagesSent).ToArray(), false);

        await FollowupAsync(embed: listEmbed);
    }


    [SlashCommand("clear-characters", "Remove all characters from the current channel")]
    public async Task ClearCharacters()
    {
        await RespondAsync(embed: MP.WAIT_MESSAGE);

        var spawnedCharacters = await DatabaseHelper.GetAllSpawnedCharactersInChannelAsync(Context.Channel.Id);

        foreach (var spawnedCharacter in spawnedCharacters)
        {
            var deleteSpawnedCharacterAsync = DatabaseHelper.DeleteSpawnedCharacterAsync(spawnedCharacter);
            MemoryStorage.CachedCharacters.Remove(spawnedCharacter.Id);

            try
            {
                var webhookClient = MemoryStorage.CachedWebhookClients.Find(spawnedCharacter.WebhookId);
                if (webhookClient is not null)
                {
                    await webhookClient.DeleteWebhookAsync();
                }
            }
            catch
            {
                // care not
            }

            MemoryStorage.CachedWebhookClients.Remove(spawnedCharacter.WebhookId);
            await deleteSpawnedCharacterAsync;
        }

        var message = $"{MP.OK_SIGN_DISCORD} Successfully removed {spawnedCharacters.Count} characters from the current channel";
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green));
    }
}
