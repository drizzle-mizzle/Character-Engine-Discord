using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

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
        // await WatchDog.BlockUserGloballyAsync(userId); TODO: Fix

        await command.FollowupAsync($"{MessagesTemplates.OK_SIGN_DISCORD} User {userId} blocked");
    }


    public async Task UnblockUserAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var userId = (ulong)command.Data.Options.First().Value;
        var result = await WatchDog.UnblockUserGloballyAsync(userId);

        await command.FollowupAsync($"{MessagesTemplates.OK_SIGN_DISCORD} User {userId} {(result ? "was removed from" : "is not in")} the blacklist");
    }


    public async Task ReportMetricsAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var range = (int)command.Data.Options.First(o => o.Name == "range").Value;
        var rangeType = (string)command.Data.Options.First(o => o.Name == "range-type").Value;

        Metric[] metrics;
        await using var db = DatabaseHelper.GetDbContext();
        
        if (rangeType == "all-time")
        {
            metrics = await db.Metrics.ToArrayAsync();
        }
        else
        {
            var dt = DateTime.Now - rangeType switch
            {
                "minutes" => new TimeSpan(0, minutes: range, 0),
                "hours" => new TimeSpan(hours: range, 0, 0),
                "days" => new TimeSpan(days: range, 0, 0, 0),
                _ => throw new ArgumentOutOfRangeException()
            };

            metrics = await db.Metrics.Where(m => m.CreatedAt >= dt).ToArrayAsync();
        }

        var metricsReport = MessagesHelper.GetMetricsReport(metrics);

        await command.FollowupAsync(embed: metricsReport.ToInlineEmbed(Color.LighterGrey, false));
    }
}
