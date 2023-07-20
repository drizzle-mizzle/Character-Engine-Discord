using static CharacterEngineDiscord.Services.IntegrationsService;

namespace CharacterEngineDiscord.Models
{
    public class SearchQuery
    {
        public ulong ChannelId { get; }
        public ulong AuthorId { get; }
        public IntegrationType IntegrationType { get; }
        public SearchQueryData SearchQueryData { get; }
        public int Pages { get; }
        public int CurrentRow { get; set; }
        public int CurrentPage { get; set; }
        public DateTime CreatedAt { get; }

        public SearchQuery(ulong channelId, ulong authorId, SearchQueryData data, int pages)
        {
            ChannelId = channelId;
            SearchQueryData = data;
            Pages = pages;
            CurrentRow = 1;
            CurrentPage = 1;
            CreatedAt = DateTime.UtcNow;
            AuthorId = authorId;
        }
    }
}
