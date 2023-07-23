namespace CharacterEngineDiscord.Models.CharacterHub
{
    public class ChubSearchParams
    {
        /// <summary>
        /// Search prompt
        /// </summary>
        public string Text { get; set; } = "";

        /// <summary>
        /// Characters per page
        /// </summary>
        public int Amount { get; set; } = 10;

        /// <summary>
        /// E.g. "Maid,NSFW, Game Characters"
        /// </summary>
        public string Tags { get; set; } = "";

        /// <summary>
        /// No idea if it actually works, but it was in API. Probably works the same way as Tags.
        /// </summary>
        public string ExcludeTags { get; set; } = "";

        /// <summary>
        /// If amount = 10 and page = 1, search will return results from 1 to 10,
        /// If amount = 12 and page = 2, will show results from 13 to 24 and so on...
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Results sorting order
        /// </summary>
        public SortField SortBy { get; set; } = SortField.Latest;
        /// <summary>
        /// As it says
        /// </summary>
        public bool AllowNSFW { get; set; } = true;

        /// <summary>
        /// Doesn't seem to work on the site itself, but probably will here, or probably won't
        /// </summary>
        public bool OnlyNSFW { get; set; } = false;

        /// <summary>
        /// You don't need it
        /// </summary>
        public string SortFieldValue
        {
            get
            {
                return (int)SortBy switch
                {
                    1 => "id",
                    2 => "name",
                    3 => "rating",
                    4 => "star_count",
                    5 => "created_at",
                    6 => "last_activity_at",
                    7 => "random",
                    8 => "rating_count",
                    9 => "trending_downloads",
                    10 => "n_tokens",
                    11 => "download_count",
                    _ => "rating"
                };
            }
        }
    }

    public enum SortField
    {
        Id = 1,
        Name = 2,
        Rating = 3,

        /// <summary>
        /// Stars count
        /// </summary>
        MostPopular = 4,
        Latest = 5,
        Updated = 6,
        Random = 7,
        RatingCount = 8,
        TrendingDonwloads = 9,
        Tokens = 10,
        DownloadCount = 11
    }
}
