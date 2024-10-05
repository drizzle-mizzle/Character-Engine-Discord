using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Models.Db;


public class StoredAction
{
    public StoredAction(StoredActionType storedActionType, string data, int maxAttemtps)
    {
        Id = Guid.NewGuid();
        StoredActionType = storedActionType;
        Data = data;
        CreatedAt = DateTime.Now;
        Status = StoredActionStatus.Pending;
        Attempt = 0;
        MaxAttemtps = maxAttemtps;
    }

    [Key]
    public Guid Id { get; init; }

    public StoredActionType StoredActionType { get; init; }
    public string Data { get; set; }
    public DateTime CreatedAt { get; init; }
    public StoredActionStatus Status { get; set; }
    public int Attempt { get; set; }
    public int MaxAttemtps { get; set; }
}


public enum StoredActionType
{
    SakuraAiEnsureLogin = 1
}


public enum StoredActionStatus
{
    Pending = 1,
    InProcess = 2,
    Finished = 3,
    Canceled = 0
}
