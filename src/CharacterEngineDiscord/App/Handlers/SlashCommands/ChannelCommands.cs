using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Masters;
using CharacterEngine.App.Repositories;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using static CharacterEngine.App.Helpers.ValidationsHelper;
using MP = CharacterEngine.App.Helpers.Discord.MessagesTemplates;

namespace CharacterEngine.App.Handlers.SlashCommands;


[ValidateChannelPermissions]
[Group("channel", "Configure per-channel settings")]
public class ChannelCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly CharactersRepository _charactersRepository;
    private readonly CacheRepository _cacheRepository;
    private readonly InteractionsMaster _interactionsMaster;


    public ChannelCommands(
        AppDbContext db,
        CharactersRepository charactersRepository,
        CacheRepository cacheRepository,
        InteractionsMaster interactionsMaster
    )
    {
        _db = db;
        _charactersRepository = charactersRepository;
        _cacheRepository = cacheRepository;
        _interactionsMaster = interactionsMaster;
    }


    private ulong PrimaryChannelId
        => Context.Channel is SocketThreadChannel threadChannel ? threadChannel.ParentChannel.Id : Context.Channel.Id;


    [SlashCommand("messages-format", "Default messages format for all integrations in the channel")]
    public async Task MessagesFormat(SinglePropertyAction action, string? newFormat = null, bool hide = false)
    {
        if (action is not SinglePropertyAction.show)
        {
            await ValidateAccessLevelAsync(AccessLevel.Manager, (SocketGuildUser)Context.User);
        }

        await DeferAsync(ephemeral: hide);

        string message = null!;

        switch (action)
        {
            case SinglePropertyAction.show:
            {
                message = "**Channel-wide messages format:**\n" + await _interactionsMaster.BuildChannelMessagesFormatDisplayAsync(Context.Channel.Id);
                break;
            }
            case SinglePropertyAction.update:
            {
                if (newFormat is null)
                {
                    throw new UserFriendlyException("Specify the new-format parameter");
                }

                message = await UpdateChannelMessagesFormatAsync(Context.Channel.Id, newFormat);
                break;
            }
            case SinglePropertyAction.resetDefault:
            {
                message = await UpdateChannelMessagesFormatAsync(Context.Channel.Id, null);
                break;
            }
        }

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    [SlashCommand("system-prompt", "Default system prompt for all integrations in the channel")]
    public async Task SystemPrompt(SinglePropertyAction action, string? newPrompt = null)
    {
        if (action is not SinglePropertyAction.show)
        {
            await ValidateAccessLevelAsync(AccessLevel.Manager, (SocketGuildUser)Context.User);
        }

        await DeferAsync();

        string message = null!;

        switch (action)
        {
            case SinglePropertyAction.show:
            {
                message = "**Channel-wide system prompt:**\n" + await _interactionsMaster.BuildChannelSystemPromptDisplayAsync(Context.Channel.Id);
                break;
            }
            case SinglePropertyAction.update:
            {
                if (newPrompt is null)
                {
                    throw new UserFriendlyException("Specify the new-prompt parameter");
                }

                message = await UpdateChannelSystemPromptAsync(Context.Channel.Id, newPrompt);
                break;
            }
            case SinglePropertyAction.resetDefault:
            {
                message = await UpdateChannelSystemPromptAsync(Context.Channel.Id, null);
                break;
            }
        }

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
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

        var spawnedCharacters = await _charactersRepository.GetAllSpawnedCharactersInChannelAsync(PrimaryChannelId);
        if (spawnedCharacters.Count == 0)
        {
            await FollowupAsync(embed: "This channel has no spawned characters".ToInlineEmbed(Color.Magenta));
            return;
        }

        var listEmbed = MessagesHelper.BuildCharactersList(spawnedCharacters.OrderByDescending(c => c.MessagesSent).ToArray(), false);

        await FollowupAsync(embed: listEmbed);
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("clear-characters", "Remove all characters from the current channel")]
    public async Task ClearCharacters()
    {
        await RespondAsync(embed: MP.WAIT_MESSAGE);

        var spawnedCharacters = await _charactersRepository.GetAllSpawnedCharactersInChannelAsync(PrimaryChannelId);
        var deleteSpawnedCharactersAsync = _charactersRepository.DeleteSpawnedCharactersAsync(spawnedCharacters);

        foreach (var spawnedCharacter in spawnedCharacters)
        {
            _cacheRepository.CachedCharacters.Remove(spawnedCharacter.Id);

            try
            {
                var webhookClient = _cacheRepository.CachedWebhookClients.Find(spawnedCharacter.WebhookId);
                if (webhookClient is not null)
                {
                    await webhookClient.DeleteWebhookAsync();
                }
            }
            catch
            {
                // care not
            }

            _cacheRepository.CachedWebhookClients.Remove(spawnedCharacter.WebhookId);
        }

        await deleteSpawnedCharactersAsync;

        var message = $"{MP.OK_SIGN_DISCORD} Successfully removed {spawnedCharacters.Count} characters from the current channel";
        await ModifyOriginalResponseAsync(msg => { msg.Embed = message.ToInlineEmbed(Color.Green); });
    }


    private async Task<string> UpdateChannelMessagesFormatAsync(ulong channelId, string? newFormat)
    {
       ValidateMessagesFormat(newFormat);

        var channel = await _db.DiscordChannels.FirstAsync(c => c.Id == channelId);
        channel.MessagesFormat = newFormat;
        await _db.SaveChangesAsync();

        return $"{MP.OK_SIGN_DISCORD} Channel-wide messages format {(newFormat is null ? "reset to default value" : "was changed")} successfully:\n" +
               _interactionsMaster.BuildChannelMessagesFormatDisplayAsync(channel.Id);
    }


    private async Task<string> UpdateChannelSystemPromptAsync(ulong channelId, string? newPrompt)
    {
        // ValidateMessagesFormat(newFormat);

        var channel = await _db.DiscordChannels.FirstAsync(g => g.Id == channelId);
        channel.SystemPrompt = newPrompt;
        await _db.SaveChangesAsync();

        return $"{MP.OK_SIGN_DISCORD} Channel-wide system prompt {(newPrompt is null ? "reset to default value" : "was changed")} successfully:\n" +
               _interactionsMaster.BuildChannelSystemPromptDisplayAsync(channel);
    }
}
