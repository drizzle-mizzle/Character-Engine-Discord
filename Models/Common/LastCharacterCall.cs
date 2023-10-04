namespace CharacterEngineDiscord.Models.Common
{
    internal class LastCharacterCall
    {
        public List<AvailableCharacterResponse> AvailableMessages { get; set; }
        public DateTime LastUpdated { get; set; }

        public LastCharacterCall(List<AvailableCharacterResponse> availableMessages, DateTime lastUpdated)
        {
            AvailableMessages = availableMessages;
            LastUpdated = lastUpdated;
        }
    }
}
