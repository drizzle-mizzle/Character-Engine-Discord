using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Services;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.Handlers.SlashCommands.Explicit;


public class BotAdminCommandsHandler
{
    private readonly AppDbContext _db;


    public BotAdminCommandsHandler(AppDbContext db)
    {
        _db = db;
    }


    public async Task ShutdownAsync(SocketSlashCommand command)
    {
        await command.RespondAsync(embed: "T_T".ToInlineEmbed(Color.Green));
        Environment.Exit(0);
    }


    public async Task BlockUserAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var userId = ulong.Parse(command.Data.Options.First().Value.ToString()!);
        await WatchDog.BlockUserGloballyAsync(userId, null, DateTime.Now.AddHours(1));

        await command.FollowupAsync($"{MessagesTemplates.OK_SIGN_DISCORD} User {userId} blocked");
    }


    public async Task UnblockUserAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var userId = ulong.Parse(command.Data.Options.First().Value.ToString()!);
        var result = await WatchDog.UnblockUserGloballyAsync(userId);

        await command.FollowupAsync($"{MessagesTemplates.OK_SIGN_DISCORD} User {userId} {(result ? "was removed from" : "is not in")} the blacklist");
    }


    public async Task ReportMetricsAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var rangeType = (int)(long)command.Data.Options.First(o => o.Name == "range-type").Value;

        Metric[] metrics;
        DateTime? sinceDt = null;

        if (rangeType == 0) // all-time
        {
            metrics = await _db.Metrics.ToArrayAsync();
        }
        else
        {
            var range = (int)(long)command.Data.Options.First(o => o.Name == "range").Value;
            sinceDt = DateTime.Now - rangeType switch
            {
                1 => new TimeSpan(0, minutes: range, 0), // minutes
                2 => new TimeSpan(hours: range, 0, 0), // hours
                3 => new TimeSpan(days: range, 0, 0, 0), // days
                _ => throw new ArgumentOutOfRangeException()
            };

            metrics = await _db.Metrics.Where(m => m.CreatedAt >= sinceDt).ToArrayAsync();
        }

        var metricsReport = MessagesHelper.BuildMetricsReport(metrics, sinceDt);

        await command.FollowupAsync(embed: metricsReport.ToInlineEmbed(Color.LighterGrey, false));
    }
}
