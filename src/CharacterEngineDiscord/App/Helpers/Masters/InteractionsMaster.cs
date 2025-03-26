using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Infrastructure;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Shared.Abstractions.Characters;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.Helpers.Masters;


public class InteractionsMaster
{
    private const string INHERITED_FROM_CHANNEL = "(inherited from channel-wide setting)";
    private const string INHERITED_FROM_GUILD = " (inherited from server-wide setting)";
    private const string INHERITED_FROM_DEFAULT = "(default)";
    private static readonly string DefaultMessagesFormat = BotConfig.DEFAULT_MESSAGES_FORMAT.Replace("\\n", "\\n\n");
    private static readonly string DefaultSystemPrompt = BotConfig.DEFAULT_SYSTEM_PROMPT.Replace("\\n", "\\n\n");

    private readonly AppDbContext _db;


    public InteractionsMaster(AppDbContext db)
    {
        _db = db;
    }


    public async Task<string> BuildCharacterMessagesFormatDisplayAsync(ISpawnedCharacter spawnedCharacter)
    {
        if (spawnedCharacter.MessagesFormat is string characterMessagesFormat)
        {
            return FullFormatPreview(null, characterMessagesFormat);
        }


        var channelData = await _db.DiscordChannels
                                   .Where(dc => dc.Id == spawnedCharacter.DiscordChannelId)
                                   .Select(dc => new
                                    {
                                        dc.DiscordGuildId,
                                        dc.MessagesFormat
                                    })
                                   .FirstAsync();

        if (channelData.MessagesFormat is string channelMessagesFormat)
        {
            return FullFormatPreview(INHERITED_FROM_CHANNEL, channelMessagesFormat);
        }

        var guildMessagesFormat = await _db.DiscordGuilds
                                           .Where(dg => dg.Id == channelData.DiscordGuildId)
                                           .Select(dg => dg.MessagesFormat)
                                           .FirstAsync();

        if (guildMessagesFormat is not null)
        {
            return FullFormatPreview(INHERITED_FROM_GUILD, guildMessagesFormat);
        }

        return FullFormatPreview(INHERITED_FROM_DEFAULT, DefaultMessagesFormat);
    }


    public async Task<string> BuildChannelMessagesFormatDisplayAsync(ulong discordChannelId)
    {
        var discordChannel = await _db.DiscordChannels.FindAsync(discordChannelId);
        return await BuildChannelMessagesFormatDisplayAsync(discordChannel!);
    }

    public async Task<string> BuildChannelMessagesFormatDisplayAsync(DiscordChannel discordChannel)
    {
        if (discordChannel.MessagesFormat is string channelMessagesFormat)
        {
            return FullFormatPreview(null, channelMessagesFormat);
        }

        var guildMessagesFormat = await _db.DiscordGuilds
                                           .Where(dg => dg.Id == discordChannel.DiscordGuildId)
                                           .Select(dg => dg.MessagesFormat)
                                           .FirstAsync();

        if (guildMessagesFormat is not null)
        {
            return FullFormatPreview(INHERITED_FROM_GUILD, guildMessagesFormat);
        }

        return FullFormatPreview(INHERITED_FROM_DEFAULT, DefaultMessagesFormat);
    }


    public async Task<string> BuildGuildMessagesFormatDisplayAsync(ulong discordGuildId)
    {
        var discordGuild = await _db.DiscordGuilds.FindAsync(discordGuildId);
        return BuildGuildMessagesFormatDisplay(discordGuild!);
    }

    public string BuildGuildMessagesFormatDisplay(DiscordGuild discordGuild)
    {
        if (discordGuild.MessagesFormat is not null)
        {
            return FullFormatPreview(null, discordGuild.MessagesFormat);
        }

        return FullFormatPreview(INHERITED_FROM_DEFAULT, DefaultMessagesFormat);
    }


    public async Task<string> BuildCharacterSystemPromptDisplayAsync(ISpawnedCharacter spawnedCharacter)
    {
        if (spawnedCharacter is not IAdoptedCharacter adoptedCharacter)
        {
            throw new ArgumentException();
        }

        if (adoptedCharacter.AdoptedCharacterSystemPrompt is string characterSystemPrompt)
        {
            return FullPromptPreview(null, characterSystemPrompt);
        }

        var channelData = await _db.DiscordChannels
                                   .Where(dc => dc.Id == spawnedCharacter.DiscordChannelId)
                                   .Select(dc => new
                                    {
                                        dc.DiscordGuildId,
                                        dc.SystemPrompt
                                    })
                                   .FirstAsync();

        if (channelData.SystemPrompt is string channelSystemPrompt)
        {
            return FullPromptPreview(INHERITED_FROM_CHANNEL, channelSystemPrompt);
        }

        var guildSystemPrompt = await _db.DiscordGuilds
                                         .Where(dg => dg.Id == channelData.DiscordGuildId)
                                         .Select(dg => dg.SystemPrompt)
                                         .FirstAsync();

        if (guildSystemPrompt is not null)
        {
            return FullPromptPreview(INHERITED_FROM_GUILD, guildSystemPrompt);
        }

        return FullPromptPreview(INHERITED_FROM_DEFAULT, DefaultSystemPrompt);
    }


    public async Task<string> BuildChannelSystemPromptDisplayAsync(ulong discordChannelId)
    {
        var discordChannel = await _db.DiscordChannels.FindAsync(discordChannelId);
        return await BuildChannelSystemPromptDisplayAsync(discordChannel!);
    }

    public async Task<string> BuildChannelSystemPromptDisplayAsync(DiscordChannel discordChannel)
    {
        if (discordChannel.SystemPrompt is string channelSystemPrompt)
        {
            return FullPromptPreview(null, channelSystemPrompt);
        }

        var guildSystemPrompt = await _db.DiscordGuilds
                                         .Where(dg => dg.Id == discordChannel.DiscordGuildId)
                                         .Select(dg => dg.SystemPrompt)
                                         .FirstAsync();

        if (guildSystemPrompt is not null)
        {
            return FullPromptPreview(INHERITED_FROM_GUILD, guildSystemPrompt);
        }

        return FullPromptPreview(INHERITED_FROM_DEFAULT, DefaultSystemPrompt);
    }


    public async Task<string> BuildGuildSystemPromptDisplayAsync(ulong guildId)
    {
        var discordGuild = await _db.DiscordGuilds.FindAsync(guildId);
        return BuildGuildSystemPromptDisplay(discordGuild!);
    }

    public string BuildGuildSystemPromptDisplay(DiscordGuild discordGuild)
    {
        if (discordGuild.SystemPrompt is not null)
        {
            return FullPromptPreview(null, discordGuild.SystemPrompt);
        }

        return FullPromptPreview(INHERITED_FROM_DEFAULT, DefaultSystemPrompt);
    }


    private string FullFormatPreview(string? inheritNote, string format)
    {
        var result = $"```\n{format}\n```\n" +
                     $"**Preview:**\n" +
                     MessagesHelper.BuildMessageFormatPreview(format);

        if (inheritNote is not null)
        {
            result = inheritNote + '\n' + result;
        }

        return result;
    }


    private string FullPromptPreview(string? inheritNote, string prompt)
    {
        var result = $"```\n{prompt}\n```";

        if (inheritNote is not null)
        {
            result = inheritNote + '\n' + result;
        }

        return result;
    }
}
