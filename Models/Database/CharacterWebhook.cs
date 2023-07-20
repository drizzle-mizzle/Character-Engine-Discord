using static CharacterEngineDiscord.Services.IntegrationsService;

namespace CharacterEngineDiscord.Models.Database
{
    public class CharacterWebhook
    {
        public required ulong Id { get; set; } // actual Discord webhook ID
        public required string WebhookToken { get; set; }
        public required string CallPrefix { get; set; }
        public required IntegrationType IntegrationType { get; set; }
        public required string MessagesFormat { get; set; }
        public required float ReplyChance { get; set; }
        public required int ReplyDelay { get; set; }
        public bool EnableTranslator { get; set; } = false;
        public required string TranslateLanguage { get; set; }
        public required string ActiveHistoryId { get; set; }
        public virtual List<HuntedUser> HuntedUsers { get; set; } = new();
        public virtual List<History> Histories { get; set; } = new();
        public virtual required Character Character { get; set; }
        public virtual required Channel Channel { get; set; }

        /// <summary>
        /// The last user who have called a character
        /// </summary>
        internal protected ulong LastDiscordUserCallerId { get; set; }

        /// <summary>
        /// To check if swipe buttons on the message should be handled (only the last one is active)
        /// </summary>
        internal protected ulong LastCharacterDiscordMsgId { get; set; } = 0;

        /// <summary>
        /// To be put in the new swipe fetching request (parentMessageId)
        /// </summary>
        internal protected string? LastUserMsgUuId { get; set; }

        /// <summary>
        /// To be put in the new response fetching request after swipe (primaryMessageId)
        /// </summary>
        internal protected string? LastCharacterMsgUuId { get; set; }

        /// <summary>
        /// Stored swiped messages (LastCharacterMsgUuId : (text : image url))
        /// </summary>
        internal protected Dictionary<string, KeyValuePair<string, string?>> AvailableCharacterMessages = new();

        internal protected int CurrentSwipeIndex { get; set; } = 0;
        internal protected bool SkipNextBotMessage { get; set; } = false;
    }
}
