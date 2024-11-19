using System.Diagnostics;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Microsoft.EntityFrameworkCore;
using NLog;
using SakuraAi.Client.Exceptions;

namespace CharacterEngine.App;


public static class BackgroundWorker
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static bool _running;


    public static void Run()
    {
        if (_running)
        {
            return;
        }

        _log.Info("[ Launching Background Worker ]");
        _running = true;

        RunInLoop(RunStoredActions, duration: TimeSpan.FromSeconds(20), log: false);
        RunInLoop(MetricsReport, TimeSpan.FromHours(1));
    }


    private static void RunInLoop(Func<string, Task> jobTask, TimeSpan duration, bool log = true)
    {
        _log.Info($"Starting loop {jobTask.Method.Name} with {(duration.TotalMinutes < 1 ? $"{duration.TotalSeconds}s" : $"{duration.TotalMinutes}min")} cooldown");

        Task.Run(async () =>
        {
            var sw = new Stopwatch();
            while (true)
            {
                if (sw.IsRunning)
                {
                    var waitMs = (int)(duration.TotalMilliseconds - sw.Elapsed.TotalMilliseconds);
                    if (waitMs > 0)
                    {
                        await Task.Delay(waitMs);
                    }
                }

                var traceId = CommonHelper.NewTraceId();

                if (log)
                {
                    _log.Info($"[{traceId}] JOB START: {jobTask.Method.Name}");
                }

                try
                {
                    await jobTask(traceId);
                }
                catch (Exception e)
                {
                    await CharacterEngineBot.DiscordShardedClient.ReportErrorAsync($"Exception in {jobTask.Method.Name}", null, e, traceId, writeMetric: true);
                }

                if (log)
                {
                    _log.Info($"[{traceId}] JOB END: {jobTask.Method.Name} | Elapsed: {sw.Elapsed.TotalSeconds}s | Next run in: {(duration.TotalMinutes < 1 ? $"{duration.TotalSeconds}s" : $"{duration.TotalMinutes}min")}");
                }

                sw.Restart();
            }
        });
    }


    private static void CallGiveUpFinalizer(StoredAction action, string traceId)
    {
        var finalizer = action.StoredActionType switch
        {
            StoredActionType.SakuraAiEnsureLogin => SendSakuraAuthGiveUpNotificationAsync(action)
        };

        Task.Run(async () =>
        {
            try
            {
                await finalizer;
            }
            catch (Exception e)
            {
                await CharacterEngineBot.DiscordShardedClient.ReportErrorAsync($"Error in GiveUpFinalizer for action {action.StoredActionType:G}", null, e, traceId, writeMetric: true);
            }
        });
    }


    private static async Task SendSakuraAuthGiveUpNotificationAsync(StoredAction action)
    {
        var data = action.ExtractSakuraAiLoginData();
        var source = action.ExtractDiscordSourceInfo();

        if (CharacterEngineBot.DiscordShardedClient.GetChannel(source.ChannelId) is not ITextChannel channel)
        {
            return;
        }

        var user = await channel.GetUserAsync(source.UserId);
        var msg = $"{IntegrationType.SakuraAI.GetIcon()} SakuraAI\n\nAuthorization confirmation time for account **{data.Email}** has expired.\nPlease, try again.";

        _log.Trace(msg);
        await channel.SendMessageAsync(user?.Mention ?? "@?", embed: msg.ToInlineEmbed(Color.LightOrange, bold: false));
    }



    //////////////////
    /// Job groups ///
    //////////////////

    private static readonly StoredActionType[] _quickJobActionTypes = [StoredActionType.SakuraAiEnsureLogin];
    private static async Task RunStoredActions(string traceId)
    {
        var actions = await GetPendingActionsAsync(_quickJobActionTypes, traceId);

        foreach (var action in actions)
        {
            var job = action.StoredActionType switch
            {
                StoredActionType.SakuraAiEnsureLogin => IntegrationsHelper.EnsureSakuraAiLoginAsync(action),
            };

            try
            {
                await job;
            }
            catch (SakuraException)
            {
                // care not
            }
            catch (Exception e)
            {
                await CharacterEngineBot.DiscordShardedClient.ReportErrorAsync("Exception in Quick Jobs loop", null, e, traceId, writeMetric: true);

                await using var db = DatabaseHelper.GetDbContext();
                action.Status = StoredActionStatus.Canceled;
                db.StoredActions.Update(action);
                await db.SaveChangesAsync();
            }
        }
    }


    private static async Task MetricsReport(string traceId)
    {
        MetricsWriter.LockWrite();
        Metric[] metrics;
        try
        {
            if (MetricsWriter.GetLastMetricReport() == default)
            {
                return;
            }

            await using var db = DatabaseHelper.GetDbContext();
            metrics = await db.Metrics.Where(m => m.CreatedAt >= MetricsWriter.GetLastMetricReport()).ToArrayAsync();
        }
        finally
        {
            MetricsWriter.SetLastMetricReport(DateTime.Now);
            MetricsWriter.UnlockWrite();
        }

        var metricsReport = MessagesHelper.GetMetricsReport(metrics);

        var client = CharacterEngineBot.DiscordShardedClient;
        await client.ReportLogAsync($"[{traceId}] Hourly Metrics Report", metricsReport);
    }


    private static async Task RevalidateBlockedUsers(string traceId)
    {
        await using var db = DatabaseHelper.GetDbContext();
        var blockedUserIds = await db.BlockedUsers.Where(b => b.BlockedUntil <= DateTime.Now).Select(b => b.Id).ToArrayAsync();
        foreach (var id in blockedUserIds)
        {
            try
            {
                await WatchDog.UnblockUserGloballyAsync(id);
            }
            catch (Exception e)
            {
                await CharacterEngineBot.DiscordShardedClient.ReportErrorAsync("Exception in RevalidateBlockedUsers", null, e, traceId, writeMetric: true);
            }
        }
    }



    // Helpers

    private static async Task<List<StoredAction>> GetPendingActionsAsync(StoredActionType[] actionTypes, string traceId)
    {
        var actionsToRun = new List<StoredAction>();

        await using var db = DatabaseHelper.GetDbContext();
        var storedActions = await db.StoredActions
                                    .Where(sa => sa.Status == StoredActionStatus.Pending
                                              && actionTypes.Contains(sa.StoredActionType))
                                    .ToArrayAsync();

        foreach (var action in storedActions)
        {
            if (action.Attempt <= action.MaxAttemtps)
            {
                actionsToRun.Add(action);
                continue;
            }

            var sourceInfo = action.ExtractDiscordSourceInfo();

            var title = $"Giving up on action **{action.StoredActionType:G}**";
            var msg = $"**Attempt**: {action.Attempt}\n" +
                      $"**ActionID**: {action.Id}\n" +
                      $"**UserID**: {sourceInfo.UserId}\n" +
                      $"**ChannelID**: {sourceInfo.ChannelId}";

            await CharacterEngineBot.DiscordShardedClient.ReportLogAsync(title, msg, logToConsole: true);

            action.Status = StoredActionStatus.Canceled;
            await db.SaveChangesAsync();

            CallGiveUpFinalizer(action, traceId);
        }

        return actionsToRun;
    }

}
