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


[Group("guild", "Guild-wide settings configuration")]
[ValidateAccessLevel(AccessLevels.Manager)]
[ValidateChannelPermissions]
public class GuildCommands : InteractionModuleBase<InteractionContext>
{
    private readonly DiscordSocketClient _discordClient;
    private readonly AppDbContext _db;


    public GuildCommands(DiscordSocketClient discordClient, AppDbContext db)
    {
        _discordClient = discordClient;
        _db = db;
    }


    public enum MessagesFormatAction { show, update, resetDefault }

    [SlashCommand("messages-format", "Messages format")]
    public async Task MessagesFormat(MessagesFormatAction action, string? newFormat = null)
    {
        await DeferAsync();

        var guild = await _db.DiscordGuilds.FirstAsync(g => g.Id == Context.Guild.Id);

        var message = string.Empty;
        switch (action)
        {
            case MessagesFormatAction.show:
            {
                var format = (guild.MessagesFormat ?? BotConfig.DEFAULT_MESSAGES_FORMAT).Replace("\\n", "\\n\n");
                var preview = MH.BuildMessageFormatPreview(format);
                message = $"**Current guild messages format{(guild.MessagesFormat is null ? " (default)" : "")}**:\n" +
                          $"```{format}```\n" +
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

                guild.MessagesFormat = newFormat;
                await _db.SaveChangesAsync();

                var preview = MH.BuildMessageFormatPreview(newFormat);
                message = $"{MessagesTemplates.OK_SIGN_DISCORD} Messages format was changed successfully.\n" +
                          $"**Preview:**\n{preview}";

                break;
            }
            case MessagesFormatAction.resetDefault:
            {
                guild.MessagesFormat = null;
                await _db.SaveChangesAsync();

                var preview = MH.BuildMessageFormatPreview(BotConfig.DEFAULT_MESSAGES_FORMAT);
                message = $"{MessagesTemplates.OK_SIGN_DISCORD} Messages format was reset to default value successfully.\n" +
                          $"**Preview:**\n{preview}";

                break;
            }
        }

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }
}
