using CharacterEngine.Models;
using SakuraAi.Models.Common;

namespace CharacterEngine.Helpers.Common;


public static class StoredActionsHelper
{
    public static string CreateSakuraAiEnsureLoginData(SignInAttempt attempt, ulong channelId, ulong userId)
        => $"{attempt.AttemptId};{attempt.ClientId};{channelId};{userId}";


    public static SakuraAiEnsureLoginData ParseSakuraAiEnsureLoginData(string data)
    {
        var parts = data.Split(';');

        return new SakuraAiEnsureLoginData(new SignInAttempt { AttemptId = parts[0], ClientId = parts[1] }, ulong.Parse(parts[2]), ulong.Parse(parts[3]));
    }


}
