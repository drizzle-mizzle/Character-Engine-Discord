using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands.Explicit;


public class BotAdminCommandsHandler
{

    // public BotAdminCommandsHandler(AppDbContext db, DiscordShardedClient discordClient, InteractionService interactions)
    // {
    //     _db = db;
    //     _discordClient = discordClient;
    //     _interactions = interactions;
    // }


    public async Task ShutdownAsync(SocketSlashCommand command)
    {
        await command.RespondAsync(embed: "T_T".ToInlineEmbed(Color.Green));
        Environment.Exit(0);
    }


    public async Task BlockUserAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var userId = (ulong)command.Data.Options.First().Value;
        await WatchDog.BlockUserGloballyAsync(userId);

        await command.FollowupAsync($"{MessagesTemplates.OK_SIGN_DISCORD} User {userId} blocked");
    }


    public async Task UnblockUserAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var userId = (ulong)command.Data.Options.First().Value;
        var result = await WatchDog.UnblockUserGloballyAsync(userId);

        await command.FollowupAsync($"{MessagesTemplates.OK_SIGN_DISCORD} User {userId} {(result ? "was removed from" : "is not in")} the blacklist");
    }
}
