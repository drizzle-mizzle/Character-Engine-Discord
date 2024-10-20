using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngineDiscord.Models;


public record SearchQuery
{
    public IntegrationType IntegrationType { get; }

    public ulong ChannelId { get; }
    public ulong UserId { get; }
    public string OriginalQuery { get; }

    public int Pages { get; }
    public int CurrentRow { get; set; }
    public int CurrentPage { get; set; }

    public DateTime CreatedAt { get; } = DateTime.Now;

    public ICollection<CommonCharacter> Characters { get; }
    public CommonCharacter SelectedCharacter
        => Characters.ElementAt(CurrentRow + ((CurrentPage - 1) * 10) - 1);



    public SearchQuery(ulong channelId, ulong userId, string query, ICollection<CommonCharacter> characters, IntegrationType type)
    {
        ChannelId = channelId;
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
