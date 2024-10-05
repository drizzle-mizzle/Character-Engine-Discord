using System.Diagnostics;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Common;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Db;
using CharacterEngineDiscord.Models.Db.Integrations;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SakuraAi.Client;
using SakuraAi.Client.Exceptions;

namespace CharacterEngine.App;


public class BackgroundWorker
{
    private readonly Logger _log;
    private readonly SakuraAiClient _sakuraAiClient;
    private readonly DiscordSocketClient _discordClient;


    public static void Run(IServiceProvider services)
    {
        var worker = new BackgroundWorker(services);
        var jobs = new List<Func<Task>> { worker.QuickJobs };

        Parallel.ForEach(jobs, job => worker.RunInLoop(job, cooldownSeconds: 5));
    }


    private BackgroundWorker(IServiceProvider services)
    {
        _log = (services.GetRequiredService<ILogger>() as Logger)!;
        _sakuraAiClient = services.GetRequiredService<SakuraAiClient>();
        _discordClient = services.GetRequiredService<DiscordSocketClient>();
    }


    private void RunInLoop(Func<Task> jobTask, int cooldownSeconds)
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

                _log.Info($"WORKER START: {jobTask.Method.Name}");

                sw.Restart();

                try
                {
                    await jobTask();
                }
                catch (Exception e)
                {
                    await _discordClient.ReportErrorAsync($"Exception in {jobTask.Method.Name}: {e}", e);
                }

                _log.Info($"WORKER END: {jobTask.Method.Name} | Elapsed: {sw.Elapsed.TotalSeconds}s | Next run in: {cooldownSeconds}s");

                sw.Restart();
            }
        });
    }


    private void CallGiveUpFinalizer(StoredAction action)
    {
        var finalizer = action.StoredActionType switch
        {
            StoredActionType.SakuraAiEnsureLogin => SendSakuraAuthGiveUpNotificationAsync(action),
        };

        Task.Run(async () =>
        {
            try
            {
                await finalizer;
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync($"Error in GiveUpFinalizer for action {action.StoredActionType:G}", e);
            }
        });
    }


    private async Task SendSakuraAuthGiveUpNotificationAsync(StoredAction action)
    {
        var data = action.ExtractSakuraAiLoginData();
        var source = action.ExtractDiscordSourceInfo();

        var getChannelTask = _discordClient.GetChannelAsync(source.ChannelId);
        if (await getChannelTask is not ITextChannel channel)
        {
            return;
        }

        var user = await channel.GetUserAsync(source.UserId);
        var msg = $"{MessagesTemplates.SAKURA_EMOJI} SakuraAI\n\nAuthorization confirmation time for account **{data.Email}** has expired.\nPlease, try again.";

        _log.Trace(msg);
        await channel.SendMessageAsync(user?.Mention ?? "@?", embed: msg.ToInlineEmbed(Color.LightOrange, bold: false));
    }



    //////////////////
    /// Job groups ///
    //////////////////

    private static readonly StoredActionType[] _quickJobActionTypes = [StoredActionType.SakuraAiEnsureLogin];
    private async Task QuickJobs()
    {
        var actions = await GetPendingActionsAsync(_quickJobActionTypes);

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
            catch (SakuraAiException sae)
            {
                await _discordClient.ReportErrorAsync("SakuraAiException", sae);
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync("Exception in Quick Jobs loop", e);

                await using var db = new AppDbContext(BotConfig.DATABASE_CONNECTION_STRING);
                action.Status = StoredActionStatus.Canceled;
                db.StoredActions.Update(action);
                await db.SaveChangesAsync();
            }
        }
    }


    private async Task<List<StoredAction>> GetPendingActionsAsync(StoredActionType[] actionTypes)
    {
        var actionsToRun = new List<StoredAction>();

        await using var db = new AppDbContext(BotConfig.DATABASE_CONNECTION_STRING);
        var storedActions = await db.StoredActions
                                    .Where(sa => sa.Status == StoredActionStatus.Pending &&
                                                 actionTypes.Contains(sa.StoredActionType))
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
            await _discordClient.ReportLogAsync(title, msg);

            action.Status = StoredActionStatus.Canceled;
            await db.SaveChangesAsync();

            CallGiveUpFinalizer(action);
        }

        return actionsToRun;
    }


    ////////////
    /// Jobs ///
    ////////////

    private async Task EnsureSakuraAiLoginAsync(StoredAction action)
    {
        await using var db = new AppDbContext(BotConfig.DATABASE_CONNECTION_STRING);

        var signInAttempt = action.ExtractSakuraAiLoginData();
        var result = await _sakuraAiClient.EnsureLoginByEmailAsync(signInAttempt);
        if (result is null)
        {
            action.Attempt++;
            db.StoredActions.Update(action);
            await db.SaveChangesAsync();

            return;
        }

        var sourceInfo = action.ExtractDiscordSourceInfo();
        var channel = (ITextChannel)await _discordClient.GetChannelAsync(sourceInfo.ChannelId)!;

        // TODO: EnsureGuildInDb

        var existingGuildIntegraion = await db.DiscordGuildIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == channel.GuildId);
        if (existingGuildIntegraion is not null)
        {
            var sakuraIntegraion = await db.SakuraAiIntegrations.FirstAsync(i => i.Id == existingGuildIntegraion.IntegraionId);
            db.SakuraAiIntegrations.Remove(sakuraIntegraion);
            db.DiscordGuildIntegrations.Remove(existingGuildIntegraion);
        }

        var newSakuraIntegration = new SakuraAiIntegration
        {
            Id = Guid.NewGuid(),
            Email = signInAttempt.Email,
            RefreshToken = result.RefreshToken,
            GlobalMessagesFormat = "",
            CreatedAt = DateTime.Now
        };

        var newLink = new DiscordGuildIntegration
        {
            Id = Guid.NewGuid(),
            DiscordGuildId = channel.GuildId,
            IntegraionId = newSakuraIntegration.Id,
            IntegrationType = IntegrationType.SakuraAi
        };

        await db.SakuraAiIntegrations.AddAsync(newSakuraIntegration);
        await db.DiscordGuildIntegrations.AddAsync(newLink);

        action.Attempt++;
        action.Status = StoredActionStatus.Finished;
        db.StoredActions.Update(action);

        await db.SaveChangesAsync();

        var user = await channel.GetUserAsync(sourceInfo.UserId);
        var msg = $"**{MessagesTemplates.SAKURA_EMOJI} SakuraAI user authorized**\n\n" +
                  $"Username: **{result.Username}**\n" +
                  "From now on, this account will be used for all **SakuraAi**-related interactions on this server. For the next step, use **/spawn-character integration-type:SakuraAi** command to spawn SakuraAI character.";

        await channel.SendMessageAsync(user.Mention, embed: msg.ToInlineEmbed(Color.Green, bold: false, result.UserImageUrl, imageAsThumb: true));
    }

}
