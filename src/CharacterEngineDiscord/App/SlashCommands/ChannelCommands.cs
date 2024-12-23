using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using MP = CharacterEngine.App.Helpers.Discord.MessagesTemplates;

namespace CharacterEngine.App.SlashCommands;


[ValidateChannelPermissions]
[Group("channel", "Configure per-channel settings")]
public class ChannelCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;


    public ChannelCommands(AppDbContext db)
    {
        _db = db;
    }


    private ulong PrimaryChannelId
        => Context.Channel is SocketThreadChannel threadChannel ? threadChannel.ParentChannel.Id : Context.Channel.Id;


    [SlashCommand("messages-format", "Messages format")]
    public async Task MessagesFormat(MessagesFormatAction action, string? newFormat = null, bool hide = false)
    {
        if (action is not MessagesFormatAction.show)
        {
            await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.Manager, (SocketGuildUser)Context.User);
        }

        await DeferAsync(ephemeral: hide);

        string message = null!;

        switch (action)
        {
            case MessagesFormatAction.show:
            {
                message = await InteractionsHelper.GetChannelMessagesFormatAsync(Context.Channel.Id, Context.Guild.Id);
                break;
            }
            case MessagesFormatAction.update:
            {
                if (newFormat is null)
                {
                    throw new UserFriendlyException("Specify the new-format parameter");
                }

                message = await InteractionsHelper.UpdateChannelMessagesFormatAsync(Context.Channel.Id, newFormat);
                break;
            }
            case MessagesFormatAction.resetDefault:
            {
                message = await InteractionsHelper.UpdateChannelMessagesFormatAsync(Context.Channel.Id, null);
                break;
            }
        }

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    [ValidateAccessLevel(AccessLevels.Manager)]
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
    public async Task ListCharacters(bool hide = false)
    {
        await DeferAsync(ephemeral: hide);

        var spawnedCharacters = await DatabaseHelper.GetAllSpawnedCharactersInChannelAsync(PrimaryChannelId);
        if (spawnedCharacters.Count == 0)
        {
            await FollowupAsync(embed: "This channel has no spawned characters".ToInlineEmbed(Color.Magenta));
            return;
        }

        var listEmbed = MessagesHelper.BuildCharactersList(spawnedCharacters.OrderByDescending(c => c.MessagesSent).ToArray(), false);

        await FollowupAsync(embed: listEmbed);
    }


    [ValidateAccessLevel(AccessLevels.Manager)]
    [SlashCommand("clear-characters", "Remove all characters from the current channel")]
    public async Task ClearCharacters()
    {
        await RespondAsync(embed: MP.WAIT_MESSAGE);

        var spawnedCharacters = await DatabaseHelper.GetAllSpawnedCharactersInChannelAsync(PrimaryChannelId);

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
        await ModifyOriginalResponseAsync(msg => { msg.Embed = message.ToInlineEmbed(Color.Green); });
    }
}
