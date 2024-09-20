namespace CharacterEngineDiscord.Models.Db;


public class StoredAction
{
    public StoredAction(Guid id, StoredActionType storedActionType, string data)
    {
        Id = id;
        StoredActionType = storedActionType;
        Data = data;
        CreatedAt = DateTime.Now;
        Status = StoredActionStatus.Pending;
        Attempt = 0;
    }


    public StoredAction(StoredActionType storedActionType, string data)
    {
        Id = Guid.NewGuid();
        StoredActionType = storedActionType;
        Data = data;
        CreatedAt = DateTime.Now;
        Status = StoredActionStatus.Pending;
        Attempt = 0;
    }


    public Guid Id { get; set; }

    public StoredActionType StoredActionType { get; set; }

    public string Data { get; set; }

    public DateTime CreatedAt { get; set; }

    public StoredActionStatus Status { get; set; }

    public int Attempt { get; set; }
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
