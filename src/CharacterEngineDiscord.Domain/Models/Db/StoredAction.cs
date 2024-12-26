using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db;


[PrimaryKey(nameof(Id))]
[Index(nameof(Status), nameof(StoredActionType), IsUnique = false)]
public class StoredAction
{
    public StoredAction(StoredActionType storedActionType, string data, int maxAttemtps)
    {
        StoredActionType = storedActionType;
        Data = data;
        CreatedAt = DateTime.Now;
        Status = StoredActionStatus.Pending;
        Attempt = 0;
        MaxAttemtps = maxAttemtps;
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public StoredActionType StoredActionType { get; init; }
    public string Data { get; init; }
    public DateTime CreatedAt { get; init; }
    public StoredActionStatus Status { get; set; }
    public int Attempt { get; set; }
    public int MaxAttemtps { get; init; }
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
