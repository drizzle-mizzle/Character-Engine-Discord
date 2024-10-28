using System.ComponentModel;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;

namespace CharacterEngine.App.SlashCommands;

[Group("conf", "Characters configuration")]
[DeferAndValidatePermissions]
[ValidateAccessLevel(AccessLevels.Manager)]
public class ConfigureCommands : InteractionModuleBase<InteractionContext>
{
    private readonly DiscordSocketClient _discordClient;
    private readonly AppDbContext _db;
    private const string ANY_IDENTIFIER_DESC = "Character call prefix or User ID or Character ID";

    public ConfigureCommands(DiscordSocketClient discordClient, AppDbContext db)
    {
        _discordClient = discordClient;
        _db = db;
    }


    public enum MessagesFormatAction { show, update, resetDefalt }

    [SlashCommand("messages-format", "Messages format")]
    public async Task MessagesFormat([Description(ANY_IDENTIFIER_DESC)] string anyIdentifier, MessagesFormatAction action, string? newFormat = null)
    {
        await FollowupAsync(embed: MessagesTemplates.WAIT_MESSAGE);

        var cachedCharacter = MemoryStorage.CachedCharacters.Find(anyIdentifier, Context.Channel.Id);

        if (cachedCharacter is null)
        {
            throw new UserFriendlyException("Character not found");
        }

        var spawnedCharacter = await DatabaseHelper.GetSpawnedCharacterByIdAsync(cachedCharacter.Id);
        if (spawnedCharacter is null)
        {
            throw new UserFriendlyException("Character not found");
        }

        var message = string.Empty;
        switch (action)
        {
            case MessagesFormatAction.show:
            {
                var inherit = string.Empty;
                var format = spawnedCharacter.MessagesFormat;
                if (format is null)
                {
                    var channel = await _db.DiscordChannels.Include(c => c.DiscordGuild).FirstAsync(c => c.Id == Context.Channel.Id);
                    if (channel.MessagesFormat is not null)
                    {
                        format = channel.MessagesFormat;
                        inherit = " (inherited from channel-wide messages format setting)";
                    }
                    else
                    {
                        format = channel.DiscordGuild?.MessagesFormat ?? BotConfig.DEFAULT_MESSAGES_FORMAT;
                        inherit = " (inherited from guild-wide messages format setting)";
                    }
                }

                var preview = MH.BuildMessageFormatPreview(format);
                message = $"Current messages format for character **{((ICharacter)spawnedCharacter).CharacterName}**{inherit}:\n" +
                          $"```{format.Replace("\\n", "\\n\n")}```\n" +
                          $"**Preview:**\n{preview}";

                break;
            }
            case MessagesFormatAction.update:
            {
                if (newFormat is null)
                {
                    throw new UserFriendlyException("Specify a new messages format");
                }

                if (!newFormat.Contains(MH.MF_MSG))
                {
                    throw new UserFriendlyException($"Add {MH.MF_MSG} placeholder");
                }

                if (newFormat.Contains(MH.MF_REF_MSG))
                {
                    var iBegin = newFormat.IndexOf(MH.MF_REF_BEGIN, StringComparison.Ordinal);
                    var iEnd = newFormat.IndexOf(MH.MF_REF_END, StringComparison.Ordinal);
                    var iMsg = newFormat.IndexOf(MH.MF_REF_MSG, StringComparison.Ordinal);

                    if (iBegin == -1 || iEnd == -1 || iBegin > iMsg || iEnd < iMsg)
                    {
                        throw new UserFriendlyException($"{MH.MF_REF_MSG} placeholder can work only with {MH.MF_REF_BEGIN} and {MH.MF_REF_END} placeholders around it: `{MH.MF_REF_BEGIN} {MH.MF_REF_MSG} {MH.MF_REF_END}`");
                    }
                }

                spawnedCharacter.MessagesFormat = newFormat;
                await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

                var preview = MH.BuildMessageFormatPreview(newFormat);
                message = $"{MessagesTemplates.OK_SIGN_DISCORD} Messages format was changed successfully.\n" +
                          $"**Preview:**\n{preview}";

                break;
            }
            case MessagesFormatAction.resetDefalt:
            {
                spawnedCharacter.MessagesFormat = BotConfig.DEFAULT_MESSAGES_FORMAT;
                await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

                var preview = MH.BuildMessageFormatPreview(BotConfig.DEFAULT_MESSAGES_FORMAT);
                message = $"{MessagesTemplates.OK_SIGN_DISCORD} Messages format was reset to default value successfully.\n" +
                          $"**Preview:**\n{preview}";

                break;
            }
        }

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));

    }
}
