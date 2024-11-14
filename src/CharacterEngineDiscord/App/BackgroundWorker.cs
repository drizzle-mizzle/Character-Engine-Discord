using System.Diagnostics;
using System.Text;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Microsoft.EntityFrameworkCore;
using NLog;
using SakuraAi.Client.Exceptions;
using DI = CharacterEngine.App.Helpers.Infrastructure.DependencyInjectionHelper;

namespace CharacterEngine.App;


public class BackgroundWorker
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

        var worker = new BackgroundWorker();

        RunInLoop(worker.QuickJobs, duration: TimeSpan.FromSeconds(20));
        RunInLoop(worker.MetricsReport, TimeSpan.FromHours(1));
    }



    private static void RunInLoop(Func<string, Task> jobTask, TimeSpan duration)
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
                _log.Info($"[{traceId}] JOB START: {jobTask.Method.Name}");

                try
                {
                    await jobTask(traceId);
                }
                catch (Exception e)
                {
                    await CharacterEngineBot.DiscordShardedClient.ReportErrorAsync($"Exception in {jobTask.Method.Name}: {e}", e, traceId, writeMetric: true);
                }

                _log.Info($"[{traceId}] JOB END: {jobTask.Method.Name} | Elapsed: {sw.Elapsed.TotalSeconds}s | Next run in: {(duration.TotalMinutes < 1 ? $"{duration.TotalSeconds}s" : $"{duration.TotalMinutes}min")}");

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
                await CharacterEngineBot.DiscordShardedClient.ReportErrorAsync($"Error in GiveUpFinalizer for action {action.StoredActionType:G}", e, traceId, writeMetric: true);
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
    private async Task QuickJobs(string traceId)
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
                await CharacterEngineBot.DiscordShardedClient.ReportErrorAsync("Exception in Quick Jobs loop", e, traceId, writeMetric: true);

                await using var db = DatabaseHelper.GetDbContext();
                action.Status = StoredActionStatus.Canceled;
                db.StoredActions.Update(action);
                await db.SaveChangesAsync();
            }
        }
    }


    private DateTime LastMetricReport;
    private async Task MetricsReport(string _)
    {
        MetricsWriter.Lock();
        Metric[] metrics;
        try
        {
            if (LastMetricReport == default)
            {
                return;
            }

            await using var db = DatabaseHelper.GetDbContext();
            metrics = await db.Metrics.Where(m => m.CreatedAt >= LastMetricReport).ToArrayAsync();
        }
        finally
        {
            LastMetricReport = DateTime.Now;
            MetricsWriter.Unlock();
        }

        var guildsJoined = metrics.Count(m => m.MetricType == MetricType.JoinedGuild);
        var guildsLeft = metrics.Count(m => m.MetricType == MetricType.LeftGuild);

        var newIntegrations = metrics.Where(m => m.MetricType == MetricType.IntegrationCreated).ToArray();
        var newSakuraIntegrations = newIntegrations.Count(i => i.Payload is string payload && payload.StartsWith(IntegrationType.SakuraAI.ToString("G")));
        var newCaiIntegrations = newIntegrations.Count(i => i.Payload is string payload && payload.StartsWith(IntegrationType.CharacterAI.ToString("G")));
        var integrationsLine = $"{IntegrationType.SakuraAI.GetIcon()}:**{newSakuraIntegrations}** " +
                               $"{IntegrationType.CharacterAI.GetIcon()}:**{newCaiIntegrations}**";

        var spawnedCharacters = metrics.Count(m => m.MetricType == MetricType.CharacterSpawned);

        var calledCharactersMetrics = metrics.Where(m => m.MetricType == MetricType.CharacterCalled)
                                             .Select(m =>
                                              {
                                                  var ids = m.Payload!.Split(':');
                                                  return new { CharacterId = m.EntityId, ChannelId = ids[0], GuildId = ids[1] };
                                              })
                                             .ToArray();

        var uniqueCharacters = calledCharactersMetrics.Select(m => m.CharacterId).Distinct().ToArray();
        var uniqueChannels = calledCharactersMetrics.Select(m => m.ChannelId).Distinct().ToArray();
        var uniqueGuilds = calledCharactersMetrics.Select(m => m.GuildId).Distinct().ToArray();

        var message = $"Joined servers: **{guildsJoined}**\n" +
                      $"Left servers: **{guildsLeft}**\n" +
                      $"Integrations created: **{newIntegrations.Length}** ({integrationsLine})\n" +
                      $"Characters spawned: **{spawnedCharacters}**\n" +
                      $"Characters calls: **{calledCharactersMetrics.Length}** | Distinct: **{uniqueCharacters.Length}** character in **{uniqueChannels}** channels in **{uniqueGuilds}** servers\n";

        var client = CharacterEngineBot.DiscordShardedClient;
        await client.ReportLogAsync("Hourly Metrics Report", message);
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
