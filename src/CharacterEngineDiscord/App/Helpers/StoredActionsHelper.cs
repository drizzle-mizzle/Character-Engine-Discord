using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Db;
using SakuraAi.Client.Models.Common;

namespace CharacterEngine.App.Helpers;


public static class StoredActionsHelper
{
    public static string CreateSakuraAiEnsureLoginData(SakuraSignInAttempt attempt, ulong channelId, ulong userId)
        => $"{channelId}~{userId}~{attempt.Id}~{attempt.Email}~{attempt.Cookie};";


    public static SakuraSignInAttempt ExtractSakuraAiLoginData(this StoredAction action)
    {
        if (action.StoredActionType is StoredActionType.SakuraAiEnsureLogin)
        {
            var parts = action.Data.Split('~');
            return new SakuraSignInAttempt
            {
                Id = parts[2],
                Email = parts[3],
                Cookie = parts[4]
            };
        }

        throw new ArgumentException("Not Discord based action source");
    }


    public static ActionSourceDiscordInfo ExtractDiscordSourceInfo(this StoredAction action)
    {
        if (action.StoredActionType is StoredActionType.SakuraAiEnsureLogin)
        {
            var parts = action.Data.Split('~');
            return new ActionSourceDiscordInfo(ChannelId: ulong.Parse(parts[0]), UserId: ulong.Parse(parts[1]));
        }

        throw new ArgumentException("Not Discord based action source");
    }
}
