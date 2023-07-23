using CharacterEngineDiscord.Models.Database;
using static CharacterEngineDiscord.Services.IntegrationsService;

namespace CharacterEngineDiscord.Models.Common
{
    public class SearchQueryData
    {
        public IntegrationType IntegrationType { get; }
        public List<Character> Characters { get; }
        public string OriginalQuery { get; }
        public string? ErrorReason { get; set; }
        public bool IsSuccessful => ErrorReason == null;
        public bool IsEmpty => !Characters.Any();

        public SearchQueryData(List<Character> characters, string query, IntegrationType type)
        {
            IntegrationType = type;
            Characters = characters;
            OriginalQuery = query;
        }
    }
}
