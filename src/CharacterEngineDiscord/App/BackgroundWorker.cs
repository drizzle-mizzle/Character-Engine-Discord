using System.Diagnostics;
using CharacterEngine.Helpers;
using CharacterEngine.Helpers.Common;
using CharacterEngine.Helpers.Discord;
using CharacterEngineDiscord.Db;
using CharacterEngineDiscord.Db.Models.Db;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SakuraAi;

namespace CharacterEngine.App;


public class BackgroundWorker
{
    public static void Run(IServiceProvider services)
        => new BackgroundWorker(services).RunJobs();


    private readonly IServiceProvider _services;
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;
    private readonly SakuraAiClient _sakuraAiClient;
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();


    private BackgroundWorker(IServiceProvider services)
    {
        _services = services;
        _discordClient = services.GetRequiredService<DiscordSocketClient>();
        _interactions = services.GetRequiredService<InteractionService>();
        _sakuraAiClient = services.GetRequiredService<SakuraAiClient>();
    }


    private void RunJobs()
    {
        RunOnRepeat(QuickJobs, cooldownSeconds: 3);
    }


    private void RunOnRepeat(Func<Task> executeTaskFunc, int cooldownSeconds)
    {
        _log.Info($"Firing recursive {executeTaskFunc.Method.Name} with {cooldownSeconds}s cooldown");

        Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();

            await RunAsync();

            while (true)
            {
                if (sw.Elapsed.Seconds < cooldownSeconds)
                {
                    continue;
                }

                await RunAsync();
                sw.Restart();
            }

            async Task RunAsync()
            {
                _log.Info($"WORKER START: {executeTaskFunc.Method.Name}");

                try
                {
                    await executeTaskFunc();
                }
                catch (Exception e)
                {
                    await _discordClient.ReportErrorAsync($"Exception in {{executeTaskFunc.Method.Name}}: {e}", e);
                }

                _log.Info($"WORKER END: {executeTaskFunc.Method.Name} | Time: {sw.Elapsed.Seconds}s | Next run: {cooldownSeconds}s later");
            }
        });
    }



    //////////////////
    /// Job groups ///
    //////////////////

    private static readonly StoredActionType[] _quickJobs = [StoredActionType.SakuraAiEnsureLogin];
    private async Task QuickJobs()
    {
        await using var db = new AppDbContext(BotConfig.DATABASE_CONNECTION_STRING);
        var storedActions = await db.StoredActions.Where(sa => sa.Status == StoredActionStatus.Pending && _quickJobs.Contains(sa.StoredActionType)).ToArrayAsync();

        foreach (var action in storedActions)
        {
            var job = action.StoredActionType switch
            {
                StoredActionType.SakuraAiEnsureLogin => EnsureSakuraAiAuthsAsync(action),
            };

            try
            {
                await job;
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync($"Exception in Quick Jobs loop", e);
            }
        }
    }


    ////////////
    /// Jobs ///
    ////////////

    private async Task EnsureSakuraAiAuthsAsync(StoredAction action)
    {
        var data = StoredActionsHelper.ParseSakuraAiEnsureLoginData(action.Data);
        await using var db = new AppDbContext(BotConfig.DATABASE_CONNECTION_STRING);

        if (action.Attempt > 20)
        {
            await _discordClient.ReportLogAsync($"Giving up on action **{action.StoredActionType:G}**", $"**Attempt**: {action.Attempt}\n**Id**: {action.Id}\n**UserId**: {data.UserId}\n**ChannelId**: {data.ChannelId}");

            action.Status = StoredActionStatus.Canceled;
            db.StoredActions.Update(action);
            await db.SaveChangesAsync();

            return;
        }

        var result = await _sakuraAiClient.EnsureLoginByEmailAsync(data.SignInAttempt);
        if (result is null)
        {
            action.Attempt++;
            db.StoredActions.Update(action);
            await db.SaveChangesAsync();

            return;
        }

        var channel = (ITextChannel)await _discordClient.GetChannelAsync(data.ChannelId);
        var user = await channel.GetUserAsync(data.UserId);

        var message = $"**{MessagesTemplates.SAKURA_EMOJI} SakuraAI user authorized**\n" +
                      $"Username: **{result.Username}**\n" +
                      "From now on, this account will be used for all **SakuraAi**-related interactions on this server.\n" +
                      "To spawn SakuraAi character, use **/spawn-character integration-type:SakuraAi** command.";

        await channel.SendMessageAsync(user.Mention, embed: message.ToInlineEmbed(Color.Green, false, result.UserImageUrl, true));

        action.Attempt++;
        action.Status = StoredActionStatus.Finished;
        db.StoredActions.Update(action);
        await db.SaveChangesAsync();
    }

}
