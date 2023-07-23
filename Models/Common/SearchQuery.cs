using static CharacterEngineDiscord.Services.IntegrationsService;

namespace CharacterEngineDiscord.Models.Common
{
    public class SearchQuery
    {
        public ulong ChannelId { get; }
        public ulong AuthorId { get; }
        public SearchQueryData SearchQueryData { get; }
        public int Pages { get; }
        public int CurrentRow { get; set; }
        public int CurrentPage { get; set; }
        public DateTime CreatedAt { get; }

        public SearchQuery(ulong channelId, ulong authorId, SearchQueryData data, int pages)
        {
            ChannelId = channelId;
            AuthorId = authorId;
            SearchQueryData = data;
            Pages = pages;
            CurrentRow = 1;
            CurrentPage = 1;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
