using System.Collections.Concurrent;
using CharacterEngineDiscord.Db.Models;

namespace CharacterEngine.App;


public class LocalStorage
{
    public ConcurrentBag<SearchQuery> SearchQueries { get; } = [];


}
