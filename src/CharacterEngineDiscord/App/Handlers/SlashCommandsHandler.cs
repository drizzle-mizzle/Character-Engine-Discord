using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using NLog;

namespace CharacterEngine.App.Handlers;


public class SlashCommandsHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _log;
    private readonly AppDbContext _db;

    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public SlashCommandsHandler(IServiceProvider serviceProvider, AppDbContext db, ILogger log,
                                DiscordSocketClient discordClient, InteractionService interactions)
    {
        _serviceProvider = serviceProvider;
        _log = log;
        _db = db;

        _discordClient = discordClient;
        _interactions = interactions;
    }

    private static readonly ChannelPermission[] REQUIRED_PERMS = [ ChannelPermission.ViewChannel, ChannelPermission.SendMessages, ChannelPermission.AddReactions, ChannelPermission.EmbedLinks, ChannelPermission.AttachFiles, ChannelPermission.ManageWebhooks ];


    public Task HandleSlashCommand(SocketSlashCommand command)
        => Task.Run(async () =>
        {
            try
            {
                await HandleSlashCommandAsync(command);
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync(e);
            }
        });
    

    private static readonly Embed _errMsgEmbed1 = $"{MessagesTemplates.WARN_SIGN_DISCORD} Bot can opearte only in text channels".ToInlineEmbed(Color.Red);
    private static readonly Embed _errMsgEmbed2 = $"{MessagesTemplates.WARN_SIGN_DISCORD} Bot has no permission to view this channel".ToInlineEmbed(Color.Red);
    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        if (command.Channel is not ITextChannel channel)
        {
            await command.RespondAsync(embed: _errMsgEmbed1);
            return;
        }

        var getUserTask = channel.GetUserAsync(_discordClient.CurrentUser.Id);
        if (await getUserTask is not IGuildUser botGuildUser)
        {
            await command.RespondAsync(embed: _errMsgEmbed2);
            return;
        }

        var botRoles = channel.Guild.Roles.Where(role => botGuildUser.RoleIds.Contains(role.Id)).ToArray();
        var guildPermissions = botRoles.SelectMany(role => role.Permissions.ToList()).ToArray();

        if (!guildPermissions.Contains(GuildPermission.Administrator))
        {
            var channelRoleOws = botRoles
                                .Select(role => channel.GetPermissionOverwrite(role))
                                .Where(ow => ow.HasValue)
                                .ToList()
                                .ConvertAll(ow => (OverwritePermissions)ow!);

            var channelAllowedPerms = channelRoleOws.SelectMany(ow => ow.ToAllowList()).ToList();
            var missingPerms = string.Join("\n", REQUIRED_PERMS.Where(channelPermission => !channelAllowedPerms.Contains(channelPermission) && !guildPermissions.Contains((GuildPermission)channelPermission))
                                                               .Select(p => $"> {p:G}"));

            if (missingPerms.Length != 0)
            {
                var msg = $"{MessagesTemplates.WARN_SIGN_DISCORD} **Permissions required for the bot to operate in this channel:**\n```{missingPerms}```\n";
                await command.RespondAsync(embed: msg.ToInlineEmbed(Color.Red, bold: false));
                return;
            }
        }

        await (command.CommandName switch
        {
            "start" => HandleStartCommandAsync(command),
            "disable" => HandleDisableCommandAsync(command),
            _ => _interactions.ExecuteCommandAsync(new InteractionContext(_discordClient, command, command.Channel), _serviceProvider)
        });
    }


    private async Task HandleStartCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var guild = _discordClient.Guilds.First(g => g.Id == command.GuildId);
        var disableCommand = InteractionsHelper.BuildDisableCommand();

        var commands = await guild.GetApplicationCommandsAsync();

        foreach (var installedCommand in commands)
        {
            await installedCommand.DeleteAsync();
        }

        await guild.CreateApplicationCommandAsync(disableCommand);
        await _interactions.RegisterCommandsToGuildAsync(guild.Id, false);

        await command.FollowupAsync("OK");
    }


    private async Task HandleDisableCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var guild = _discordClient.Guilds.First(g => g.Id == command.GuildId);
        var startCommand = InteractionsHelper.BuildStartCommand();

        await guild.DeleteApplicationCommandsAsync();
        await guild.CreateApplicationCommandAsync(startCommand);

        await command.FollowupAsync("OK");
    }
    
}
