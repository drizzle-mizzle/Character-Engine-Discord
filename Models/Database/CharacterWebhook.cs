using System.Security.Permissions;
using System.Security.Policy;
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
        public required bool SwipesEnabled { get; set; }
        public required bool CrutchEnabled { get; set; }
        public required int ResponseDelay { get; set; }
        public required IntegrationType IntegrationType { get; set; }
        public required string? MessagesFormat { get; set; }
        public required float ReplyChance { get; set; }
        public required DateTime LastCallTime { get; set; }

        // CharacterAI
        public string? PersonalCaiUserAuthToken { get; set; }
        public required string? CaiActiveHistoryId { get; set; }

        // OpenAI ChatGPT
        public string? PersonalOpenAiApiEndpoint { get; set; }
        public string? PersonalOpenAiApiToken { get; set; }
        public required string? OpenAiModel { get; set; }
        public required float? OpenAiFreqPenalty { get; set; }
        public required float? OpenAiPresencePenalty { get; set; }
        public required float? OpenAiTemperature { get; set; }
        public required int? OpenAiMaxTokens { get; set; }

        // Universal Tavern (except CharacterAI)
        public required string? UniversalJailbreakPrompt { get; set; }
        public int LastRequestTokensUsage { get; set; } = 0;

        public required string CharacterId { get; set; }
        public virtual Character Character { get; set; } = null!;
        public required ulong ChannelId { get; set; }
        public virtual Channel Channel { get; set; } = null!;

        public virtual List<OpenAiHistoryMessage> OpenAiHistoryMessages { get; set; } = new();
        public virtual List<HuntedUser> HuntedUsers { get; set; } = new();

        /// <summary>
        /// The last user who have called a character
        /// </summary>
        public ulong LastDiscordUserCallerId { get; set; } = 0;

        /// <summary>
        /// To check if swipe buttons on the message should be handled (only the last one is active)
        /// </summary>
        public ulong LastCharacterDiscordMsgId { get; set; } = 0;

        /// <summary>
        /// To be put in the new swipe fetching request (parentMessageId)
        /// </summary>
        public string? LastUserMsgUuId { get; set; }

        /// <summary>
        /// To be put in the new response fetching request after swipe (primaryMessageId)
        /// </summary>
        public string? LastCharacterMsgUuId { get; set; }

        public int CurrentSwipeIndex { get; set; } = 0;
        public bool SkipNextBotMessage { get; set; } = false;
    }
}
