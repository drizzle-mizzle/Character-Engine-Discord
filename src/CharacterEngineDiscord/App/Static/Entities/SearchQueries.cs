using System.Collections.Concurrent;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngine.App.Static.Entities;


public sealed class SearchQueryCollection
{
    private readonly ConcurrentDictionary<ulong, SearchQuery> _searchQueries = [];


    public void Add(SearchQuery searchQuery)
    {
        Remove(searchQuery.MessageId);
        _searchQueries.TryAdd(searchQuery.MessageId, searchQuery);
    }

    public void Remove(ulong messageId)
    {
        _searchQueries.TryRemove(messageId, out _);
    }

    public SearchQuery? Find(ulong messageId)
        => _searchQueries.GetValueOrDefault(messageId);
}


public record SearchQuery
{
    public IntegrationType IntegrationType { get; }

    public ulong MessageId { get; }
    public ulong UserId { get; }
    public string OriginalQuery { get; }

    public int Pages { get; }
    public int CurrentRow { get; set; }
    public int CurrentPage { get; set; }

    public DateTime CreatedAt { get; } = DateTime.Now;

    public ICollection<CommonCharacter> Characters { get; }

    public CommonCharacter SelectedCharacter
        => Characters.ElementAt(CurrentRow + ((CurrentPage - 1) * 10) - 1);


    public SearchQuery(ulong messageId, ulong userId, string query, ICollection<CommonCharacter> characters, IntegrationType type)
    {
        MessageId = messageId;
        UserId = userId;
        OriginalQuery = query;
        Characters = characters;
        IntegrationType = type;

        Pages = 1;
        CurrentRow = 1;
        CurrentPage = 1;

        var cc = Characters.Count;
        while (cc / 10f > 1f)
        {
            Pages++;
            cc -= 10;
        }
    }
}
