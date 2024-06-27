using System.Diagnostics;
using CharacterEngine.Abstractions;
using CharacterEngine.Database;
using CharacterEngine.Helpers.Common;
using CharacterEngine.Helpers.Discord;
using CharacterEngine.Models.Db;
using Discord;
using Microsoft.EntityFrameworkCore;
using SakuraAi;

namespace CharacterEngine;


public abstract class Worker : CharacterEngineBase
{
    public static void Run()
    {
        Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            while (true)
            {
                if (sw.Elapsed.Seconds < 3)
                {
                    continue;
                }

                await RunQuickJobsAsync();
                sw.Restart();
            }
        });

        //     Task.Run(async () =>
        //     {
        //         var sw = Stopwatch.StartNew();
        //
        //         while (sw.Elapsed.Minutes >= 1)
        //         {
        //             await RunQuickJobsAsync();
        //             sw.Restart();
        //         }
        //     });
    }

    private static async Task RunQuickJobsAsync()
    {
        await using var db = new AppDbContext();
        var storedActions = await db.StoredActions.Where(sa => sa.Status == StoredActionStatus.Pending).ToListAsync();

        foreach (var action in storedActions)
        {
            try
            {
                await Execute(action);
            }
            catch (Exception e)
            {
                await DiscordClient.ReportErrorAsync("Exception in Quick Jobs loop", e);
            }
        }

        await Task.Delay(5000);
    }


    private static async Task Execute(StoredAction action)
    {
        switch (action.StoredActionType)
        {
            case StoredActionType.SakuraAiEnsureLogin:
                await EnsureSakuraAiAuths(action); return;
            default:
                return;
        }
    }


    private static async Task EnsureSakuraAiAuths(StoredAction action)
    {
        var data = StoredActionsHelper.ParseSakuraAiEnsureLoginData(action.Data);
        await using var db = new AppDbContext();

        if (action.Attempt > 20)
        {
            await DiscordClient.ReportLogAsync($"Giving up on action **{action.StoredActionType:G}**", $"**Attempt**: {action.Attempt}\n**Id**: {action.Id}\n**UserId**: {data.UserId}\n**ChannelId**: {data.ChannelId}");

            action.Status = StoredActionStatus.Canceled;
            db.StoredActions.Update(action);
            await db.SaveChangesAsync();

            return;
        }

        var result = await SakuraAiClient.EnsureLoginByEmailAsync(data.SignInAttempt);
        if (result is null)
        {
            action.Attempt++;
            db.StoredActions.Update(action);
            await db.SaveChangesAsync();

            return;
        }

        var channel = (ITextChannel)await DiscordClient.GetChannelAsync(data.ChannelId);
        var user = await channel.GetUserAsync(data.UserId);

        var message = $"**{MessagesTemplates.SAKURA_EMOJI} SakuraAI user authorized**\n" +
                      $"Username: **{result.Username}**\n" +
                      "From now on, this account will be used for all **SakuraAi**-related interactions on this server.\n" +
                      "To spawn SakuraAi character, use **/spawn-character integration-type:SakuraAi** command.";

        await channel.SendMessageAsync(user.Mention, embed: message.ToInlineEmbed(Color.Green, bold: false, imageUrl: result.UserImageUrl, imageAsThumb: true));

        action.Attempt++;
        action.Status = StoredActionStatus.Finished;
        db.StoredActions.Update(action);
        await db.SaveChangesAsync();
    }
}
