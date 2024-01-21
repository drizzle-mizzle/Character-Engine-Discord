namespace CharacterEngineDiscord.Models.Common
{
    public class LastCharacterCall
    {
        public List<AvailableCharacterResponse> AvailableMessages { get; set; }
        public SemaphoreSlim Locker { get; }
        public DateTime LastUpdated { get; set; }

        public LastCharacterCall(List<AvailableCharacterResponse> availableMessages, DateTime lastUpdated)
        {
            AvailableMessages = availableMessages;
            LastUpdated = lastUpdated;
            Locker = new(1, 1);
        }
    }
}
