namespace CharacterEngineDiscord.Models.Common
{
    public class CharacterResponse
    {
        public string Text { get; }
        public string? ImageRelPath { get; }
        public bool IsSuccessful { get; }
        public string? CharacterMessageId { get; }

        public CharacterResponse(string text, bool isSuccessful, string? uuid = null, string? imageRelPath = null)
        {
            Text = text;
            IsSuccessful = isSuccessful;
            CharacterMessageId = uuid;
            ImageRelPath = imageRelPath;
        }
    }
}
