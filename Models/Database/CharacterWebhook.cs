using CharacterEngineDiscord.Models.OpenAI;
using System.ComponentModel;
using System.Data.SqlClient;
using static CharacterEngineDiscord.Services.IntegrationsService;

namespace CharacterEngineDiscord.Models.Database
{
    public class CharacterWebhook
    {
        /// <summary>
        /// Actual Discord webhook ID
        /// </summary>
        public required ulong Id { get; set; }

        /// <summary>
        /// Actual Discord webhook token
        /// </summary>
        public required string WebhookToken { get; set; }

        /// <summary>
        /// Some prefix the character will respond on
        /// </summary>
        public required string CallPrefix { get; set; }
        public required bool ReferencesEnabled { get; set; }
        public required IntegrationType IntegrationType { get; set; }

        /// <summary>
        /// String where "{{msg}}" will be replaced with user message before it will be sent to a character
        /// </summary>
        public required string MessagesFormat { get; set; }
        public required float ReplyChance { get; set; }
        public required int ReplyDelay { get; set; }

        // CharacterAI
        public string? PersonalCaiUserAuthToken { get; set; }
        public required string? CaiActiveHistoryId { get; set; }
        public virtual List<CaiHistory> CaiHistories { get; set; } = new();

        // OpenAI ChatGPT
        public string? PersonalOpenAiApiToken { get; set; }
        public required string? OpenAiModel { get; set; }
        public required float? OpenAiFreqPenalty { get; set; }
        public required float? OpenAiPresencePenalty { get; set; }
        public required float? OpenAiTemperature { get; set; }
        public required int? OpenAiMaxTokens { get; set; }

        // Universal Tavern (except CharacterAI)
        public required string? UniversalJailbreakPrompt { get; set; }

        public required string CharacterId { get; set; }
        public virtual Character Character { get; set; } = null!;
        public required ulong ChannelId { get; set; }
        public virtual Channel Channel { get; set; } = null!;

        public virtual List<OpenAiHistoryMessage> OpenAiHistoryMessages { get; set; } = new();
        public virtual List<HuntedUser> HuntedUsers { get; set; } = new();

        public int? LastRequestTokensUsage { get; set; }
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
        internal protected Dictionary<string, KeyValuePair<string, string?>> AvailableCharacterResponses = new();

        internal protected int CurrentSwipeIndex { get; set; } = 0;
        internal protected bool SkipNextBotMessage { get; set; } = false;
    }
}
