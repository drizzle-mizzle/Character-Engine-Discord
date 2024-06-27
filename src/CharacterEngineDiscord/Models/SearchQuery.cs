using CharacterEngine.Models.Abstractions;
using static CharacterEngine.Models.Enums;

namespace CharacterEngine.Models;


public class SearchQuery
{
    public ulong ChannelId { get; }
    public ulong UserId { get; }
    public string OriginalQuery { get; }

    public int Pages { get; } = 1;
    public int CurrentRow { get; set; } = 1;
    public int CurrentPage { get; set; } = 1;

    public IntegrationType IntegrationType { get; }
    public ICollection<ICommonCharacter> Characters { get; }
    public DateTime CreatedAt { get; } = DateTime.Now;


    public SearchQuery(ulong channelId, ulong userId, string query, ICollection<ICommonCharacter> characters, IntegrationType type)
    {
        ChannelId = channelId;
        UserId = userId;
        OriginalQuery = query;
        Characters = characters;
        IntegrationType = type;

        var rem = Characters.Count;
        while (rem / 10f > 1f)
        {
            Pages++;
            rem -= 10;
        }
    }
}
