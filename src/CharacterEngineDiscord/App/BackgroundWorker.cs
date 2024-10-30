using System.Diagnostics;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Db;
using CharacterEngineDiscord.Models.Db.Integrations;
using Discord;
using Microsoft.EntityFrameworkCore;
using NLog;
using SakuraAi.Client.Exceptions;
using DI = CharacterEngine.App.Helpers.Infrastructure.DependencyInjectionHelper;

namespace CharacterEngine.App;


public class BackgroundWorker
{
    private readonly ILogger _log = DI.GetLogger;
    private static bool _running = false;


    public static void Run()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        var worker = new BackgroundWorker();
        var jobs = new List<Func<string, Task>> { worker.QuickJobs };

        Parallel.ForEach(jobs, job => worker.RunInLoop(job, cooldownSeconds: 5));
    }



    private void RunInLoop(Func<string, Task> jobTask, int cooldownSeconds)
    {
        _log.Info($"Starting loop {jobTask.Method.Name} with {cooldownSeconds}s cooldown");

        Task.Run(async () =>
        {
            var sw = new Stopwatch();

            while (true)
            {
                if (sw.IsRunning && sw.Elapsed.TotalSeconds < cooldownSeconds)
                {
                    continue;
                }

                var traceId = CommonHelper.NewTraceId();

                _log.Info($"[{traceId}] JOB START: {jobTask.Method.Name}");

                sw.Restart();

                try
                {
                    await jobTask(traceId);
                }
                catch (Exception e)
                {
                    await DI.GetDiscordSocketClient.ReportErrorAsync($"Exception in {jobTask.Method.Name}: {e}", e, traceId);
                }

                _log.Info($"[{traceId}] JOB END: {jobTask.Method.Name} | Elapsed: {sw.Elapsed.TotalSeconds}s | Next run in: {cooldownSeconds}s");

                sw.Restart();
            }
        });
    }


    private void CallGiveUpFinalizer(StoredAction action, string traceId)
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
                await DI.GetDiscordSocketClient.ReportErrorAsync($"Error in GiveUpFinalizer for action {action.StoredActionType:G}", e, traceId);
            }
        });
    }


    private async Task SendSakuraAuthGiveUpNotificationAsync(StoredAction action)
    {
        var data = action.ExtractSakuraAiLoginData();
        var source = action.ExtractDiscordSourceInfo();

        var getChannelTask = DI.GetDiscordSocketClient.GetChannelAsync(source.ChannelId);
        if (await getChannelTask is not ITextChannel channel)
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
                StoredActionType.SakuraAiEnsureLogin => EnsureSakuraAiLoginAsync(action),
            };

            try
            {
                await job;
            }
            catch (SakuraException se)
            {
                await DI.GetDiscordSocketClient.ReportErrorAsync("SakuraAiException", se, traceId);
            }
            catch (Exception e)
            {
                await DI.GetDiscordSocketClient.ReportErrorAsync("Exception in Quick Jobs loop", e, traceId);

                await using var db = DatabaseHelper.GetDbContext();
                action.Status = StoredActionStatus.Canceled;
                db.StoredActions.Update(action);
                await db.SaveChangesAsync();
            }
        }
    }


    private async Task<List<StoredAction>> GetPendingActionsAsync(StoredActionType[] actionTypes, string traceId)
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
            await DI.GetDiscordSocketClient.ReportLogAsync(title, msg);

            action.Status = StoredActionStatus.Canceled;
            await db.SaveChangesAsync();

            CallGiveUpFinalizer(action, traceId);
        }

        return actionsToRun;
    }


    ////////////
    /// Jobs ///
    ////////////

    private async Task EnsureSakuraAiLoginAsync(StoredAction action)
    {
        await using var db = DatabaseHelper.GetDbContext();

        var signInAttempt = action.ExtractSakuraAiLoginData();
        var result = await MemoryStorage.IntegrationModules.SakuraAiModule.EnsureLoginByEmailAsync(signInAttempt);
        if (result is null)
        {
            action.Attempt++;
            db.StoredActions.Update(action);
            await db.SaveChangesAsync();

            return;
        }

        var sourceInfo = action.ExtractDiscordSourceInfo();
        var channel = (ITextChannel)await DI.GetDiscordSocketClient.GetChannelAsync(sourceInfo.ChannelId);

        var integration = await db.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == channel.GuildId);
        if (integration is not null)
        {
            integration.SakuraEmail = signInAttempt.Email;
            integration.SakuraSessionId = result.SessionId;
            integration.SakuraRefreshToken = result.RefreshToken;
            integration.CreatedAt = DateTime.Now;
        }
        else
        {
            var newSakuraIntegration = new SakuraAiGuildIntegration
            {
                DiscordGuildId = channel.GuildId,
                SakuraEmail = signInAttempt.Email,
                SakuraSessionId = result.SessionId,
                SakuraRefreshToken = result.RefreshToken,
                GlobalMessagesFormat = "",
                CreatedAt = DateTime.Now
            };

            await db.SakuraAiIntegrations.AddAsync(newSakuraIntegration);
        }

        action.Attempt++;
        action.Status = StoredActionStatus.Finished;
        db.StoredActions.Update(action);

        await db.SaveChangesAsync();

        var msg = $"Username: **{result.Username}**\n" +
                  "From now on, this account will be used for all SakuraAI interactions on this server.\n" +
                  "For the next step, use *`/character spawn `* command to spawn new SakuraAI character in this channel.";

        var embed = new EmbedBuilder()
                   .WithTitle($"{IntegrationType.SakuraAI.GetIcon()} SakuraAI user authorized")
                   .WithDescription(msg)
                   .WithColor(IntegrationType.SakuraAI.GetColor())
                   .WithThumbnailUrl(result.UserImageUrl);

        var user = await channel.GetUserAsync(sourceInfo.UserId);
        await channel.SendMessageAsync(user.Mention, embed: embed.Build());
    }

}
