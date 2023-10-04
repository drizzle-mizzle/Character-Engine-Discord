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
        public required IntegrationType IntegrationType { get; set; }

        /// <summary>
        /// Some prefix the character will respond on
        /// </summary>
        public required string CallPrefix { get; set; }
        public string? PersonalMessagesFormat { get; set; }
        public required int ResponseDelay { get; set; }
        public required float ReplyChance { get; set; }
        public required ulong MessagesSent { get; set; }
        public required DateTime LastCallTime { get; set; }
        public required bool ReferencesEnabled { get; set; }
        public required bool SwipesEnabled { get; set; }
        public required bool CrutchEnabled { get; set; }
        public required bool FromChub { get; set; } = true;
        public bool SkipNextBotMessage { get; set; } = false;
        public int LastRequestTokensUsage { get; set; } = 0;

        public int CurrentSwipeIndex { get; set; } = 0;

        /// <summary>
        /// The last user who called a character
        /// </summary>
        public ulong LastDiscordUserCallerId { get; set; } = 0;

        /// <summary>
        /// To check if swipe buttons on the message should be handled (only the last one is active)
        /// </summary>
        public ulong LastCharacterDiscordMsgId { get; set; } = 0;

        /// <summary>
        /// To be put in the new swipe fetching request (parentMessageId)
        /// </summary>
        public string? LastUserMsgId { get; set; }

        /// <summary>
        /// To be put in the new response fetching request after swipe (primaryMessageId)
        /// </summary>
        public string? LastCharacterMsgId { get; set; }


        // AI stuff

        /// <summary>
        /// CharacterAI
        /// </summary>
        public string? ActiveHistoryID { get; set; } 

        /// <summary>
        /// All
        /// </summary>
        public string? PersonalApiToken { get; set; }

        /// <summary>
        /// OpenAI, Kobold, Horde
        /// </summary>
        public string? PersonalApiEndpoint { get; set; }

        /// <summary>
        /// OpenAI, Kobold, Horde
        /// </summary>
        public string? PersonalApiModel { get; set; }

        /// <summary>
        /// OpenAI, Kobold, Horde
        /// </summary>
        public string? PersonalJailbreakPrompt { get; set; }

        /// <summary>
        /// Presence penalty for OpenAI && Repetition penalty for Kobold/Horde, almost same thing
        /// </summary>
        public float? GenerationPresenceOrRepetitionPenalty { get; set; }

        /// <summary>
        /// Frequency penalty for OpenAI && Repetition penalty slope for Kobold/Horde
        /// </summary>
        public float? GenerationFreqPenaltyOrRepetitionSlope { get; set; }

        /// <summary>
        /// OpenAI, Kobold, Horde
        /// </summary>
        public float? GenerationTemperature { get; set; }

        /// <summary>
        /// OpenAI, Kobold, Horde
        /// </summary>
        public int? GenerationMaxTokens { get; set; }

        /// <summary>
        /// Kobold, Horde
        /// </summary>
        public int? GenerationContextSizeTokens { get; set; }

        /// <summary>
        /// Kobold/Horde
        /// </summary>
        public float? GenerationTopP { get; set; }

        /// <summary>
        /// Kobold/Horde
        /// </summary>
        public float? GenerationTopA { get; set; }

        /// <summary>
        /// Kobold/Horde
        /// </summary>
        public int? GenerationTopK { get; set; }

        /// <summary>
        /// Kobold/Horde
        /// </summary>
        public float? GenerationTypicalSampling { get; set; }

        /// <summary>
        /// Kobold/Horde
        /// </summary>
        public float? GenerationTailfreeSampling { get; set; }


        public required string CharacterId { get; set; }
        public virtual Character Character { get; set; } = null!;
        public required ulong ChannelId { get; set; }
        public virtual Channel Channel { get; set; } = null!;

        public virtual List<StoredHistoryMessage> StoredHistoryMessages { get; set; } = new();
        public virtual List<HuntedUser> HuntedUsers { get; set; } = new();
    }
}
