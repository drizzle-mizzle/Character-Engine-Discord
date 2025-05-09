﻿using System.Diagnostics;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Masters;
using CharacterEngine.App.Repositories;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Shared;
using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SakuraAi.Client.Exceptions;

namespace CharacterEngine.App.Services;


public static class BackgroundWorker
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    private static ServiceProvider _serviceProvider = null!;
    private static bool _running;


    public static void Run(ServiceProvider serviceProvider)
    {
        if (_running)
        {
            return;
        }

        _serviceProvider = serviceProvider;

        _log.Info("[ Launching Background Worker ]");
        _running = true;

        RunInLoop(RunStoredActions, TimeSpan.FromSeconds(20), log: false);
        RunInLoop(MetricsReport, TimeSpan.FromHours(1));
        RunInLoop(RevalidateBlockedUsers, TimeSpan.FromMinutes(1), log: false);
        RunInLoop(ClearCache, TimeSpan.FromMinutes(5), log: false);
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
                    await CharacterEngineBot.DiscordClient.ReportErrorAsync($"Exception in {jobTask.Method.Name}", null, e, traceId, writeMetric: true);
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
                await CharacterEngineBot.DiscordClient.ReportErrorAsync($"Error in GiveUpFinalizer for action {action.StoredActionType:G}", null, e, traceId, writeMetric: true);
            }
        });
    }


    private static async Task SendSakuraAuthGiveUpNotificationAsync(StoredAction action)
    {
        var source = action.ExtractDiscordSourceInfo();

        if (CharacterEngineBot.DiscordClient.GetChannel(source.ChannelId) is not ITextChannel channel)
        {
            return;
        }

        var user = await channel.GetUserAsync(source.UserId);
        var msg = $"**{IntegrationType.SakuraAI.GetIcon()} SakuraAI**\n\nAuthorization confirmation time has expired. Please, try again.";

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

        await using var db = _serviceProvider.GetRequiredService<AppDbContext>();
        foreach (var action in actions)
        {
            action.Attempt++;
            var job = action.StoredActionType switch
            {
                StoredActionType.SakuraAiEnsureLogin => _serviceProvider.GetRequiredService<IntegrationsMaster>()
                                                                        .EnsureSakuraAiLoginAsync(action),
            };

            try
            {
                await job;
                action.Status = StoredActionStatus.Finished;
            }
            catch (SakuraException)
            {
                action.Status = StoredActionStatus.Pending;
            }
            catch (Exception e)
            {
                await CharacterEngineBot.DiscordClient.ReportErrorAsync("Exception in Quick Jobs loop", null, e, traceId, writeMetric: true);
                action.Status = StoredActionStatus.Canceled;
            }
            finally
            {
                db.StoredActions.Update(action);
            }
        }

        await db.SaveChangesAsync();
    }


    private static async Task MetricsReport(string traceId)
    {
        var sinceDt = MetricsWriter.GetLastAutoMetricReport();

        await using var db = _serviceProvider.GetRequiredService<AppDbContext>();
        var metricsTask = db.Metrics.Where(m => m.CreatedAt >= sinceDt).ToArrayAsync();

        MetricsWriter.SetLastAutoMetricReport(DateTime.Now);

        var metricsReport = MessagesHelper.BuildMetricsReport(await metricsTask, sinceDt);
        await CharacterEngineBot.DiscordClient.ReportLogAsync($"[{traceId}] Hourly Metrics Report", metricsReport);
    }


    private static async Task RevalidateBlockedUsers(string traceId)
    {
        await using var db = _serviceProvider.GetRequiredService<AppDbContext>();
        var blockedUserIds = await db.BlockedUsers.Where(b => b.BlockedUntil <= DateTime.Now).Select(b => b.Id).ToArrayAsync();
        foreach (var id in blockedUserIds)
        {
            try
            {
                await WatchDog.UnblockUserGloballyAsync(id);
                _log.Info($"[{traceId}] User unblocked: {id}");
            }
            catch (Exception e)
            {
                await CharacterEngineBot.DiscordClient.ReportErrorAsync("Exception in RevalidateBlockedUsers", null, e, traceId, writeMetric: true);
            }
        }
    }


    private static Task ClearCache(string traceId)
    {
        using var cacheRepo = _serviceProvider.GetRequiredService<CacheRepository>();

        var webhookIds = cacheRepo.CachedWebhookClients.GetAll().Where(c => (DateTime.Now - c.Value.LastHitAt).TotalMinutes > 10).Select(c => c.Key).ToArray();
        foreach (var webhookId in webhookIds)
        {
            cacheRepo.CachedWebhookClients.Remove(webhookId);
        }

        if (webhookIds.Length != 0)
        {
            _log.Info($"[{traceId}] Cleared {webhookIds.Length} cached webhook clients");
        }

        var messageIds = cacheRepo.ActiveSearchQueries.GetAll().Where(sq => (DateTime.Now - sq.Value.CreatedAt).TotalMinutes > 5).Select(sq => sq.Key).ToArray();
        foreach (var messageId in messageIds)
        {
            cacheRepo.ActiveSearchQueries.Remove(messageId);
        }

        if (messageIds.Length != 0)
        {
            _log.Info($"[{traceId}] Cleared {messageIds.Length} cached search queries");
        }

        var channelIds = cacheRepo.GetAllCachedChannels.Where(cc => (DateTime.Now - cc.Value.CachedAt).TotalMinutes > 10).Select(cc => cc.Key).ToArray();
        foreach (var channelId in channelIds)
        {
            cacheRepo.RemoveCachedChannel(channelId);
        }

        if (channelIds.Length != 0)
        {
            _log.Info($"[{traceId}] Cleared {channelIds.Length} cached channels");
        }

        var guildsIds = cacheRepo.GetAllCachedGuilds.Where(cg => (DateTime.Now - cg.Value).TotalMinutes > 5).Select(cc => cc.Key).ToArray();
        foreach (var guildId in guildsIds)
        {
            cacheRepo.RemoveCachedGuild(guildId);
        }

        if (guildsIds.Length != 0)
        {
            _log.Info($"[{traceId}] Cleared {guildsIds.Length} cached guilds");
        }

        var userIds = cacheRepo.GetAllCachedUsers.Where(cu => (DateTime.Now - cu.Value).TotalMinutes > 5).Select(cc => cc.Key).ToArray();
        foreach (var userId in userIds)
        {
            cacheRepo.RemoveCachedUser(userId);
        }

        if (userIds.Length != 0)
        {
            _log.Info($"[{traceId}] Cleared {userIds.Length} cached users");
        }


        return Task.CompletedTask;
    }



    // Helpers

    private static async Task<List<StoredAction>> GetPendingActionsAsync(StoredActionType[] actionTypes, string traceId)
    {
        var actionsToRun = new List<StoredAction>();

        await using var db = _serviceProvider.GetRequiredService<AppDbContext>();
        var storedActions = await db.StoredActions
                                    .Where(sa => sa.Status == StoredActionStatus.Pending
                                              && actionTypes.Contains(sa.StoredActionType))
                                    .ToArrayAsync();

        foreach (var action in storedActions)
        {
            if (action.Attempt <= action.MaxAttemtps)
            {
                action.Status = StoredActionStatus.InProcess;
                actionsToRun.Add(action);
                continue;
            }

            var sourceInfo = action.ExtractDiscordSourceInfo();

            var title = $"Giving up on action **{action.StoredActionType:G}**";
            var msg = $"**Attempt**: {action.Attempt}\n" +
                      $"**UserID**: {sourceInfo.UserId}\n" +
                      $"**ChannelID**: {sourceInfo.ChannelId}";

            await CharacterEngineBot.DiscordClient.ReportLogAsync(title, msg, logToConsole: true);

            action.Status = StoredActionStatus.Canceled;
            await db.SaveChangesAsync();

            CallGiveUpFinalizer(action, traceId);
        }

        await db.SaveChangesAsync();

        return actionsToRun;
    }

}
