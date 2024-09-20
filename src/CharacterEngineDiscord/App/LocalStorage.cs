using System.Collections.Concurrent;
using CharacterEngineDiscord.Models.Local;

namespace CharacterEngine.App;


public class LocalStorage
{
    public ConcurrentBag<SearchQuery> SearchQueries { get; } = [];




}
