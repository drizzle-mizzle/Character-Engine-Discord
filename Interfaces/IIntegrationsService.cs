using CharacterAI;
using CharacterEngineDiscord.Models.CharacterHub;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Database;
using CharacterEngineDiscord.Models.KoboldAI;
using CharacterEngineDiscord.Models.OpenAI;
using CharacterEngineDiscord.Services.AisekaiIntegration;
using CharacterEngineDiscord.Services;
using Discord.Commands;
using Discord.Interactions;
using Discord.Webhook;
using Discord.WebSocket;
using Discord;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CharacterEngineDiscord.Interfaces
{
    public interface IIntegrationsService
    {
        public ulong MessagesSent { get; set; }
        public List<SearchQuery> SearchQueries { get; }
        public SemaphoreSlim SearchQueriesLock { get; }
        public HttpClient ImagesHttpClient { get; }
        public HttpClient ChubAiHttpClient { get; }
        public HttpClient CommonHttpClient { get; }

        public AisekaiClient AisekaiClient { get; }
        public CharacterAIClient? CaiClient { get; set; }

        /// <summary>
        /// Webhook ID : WebhookClient
        /// </summary>
        public Dictionary<ulong, DiscordWebhookClient> WebhookClients { get; }

        /// <summary>
        /// Stored swiped messages (Character-webhook ID : LastCharacterCall)
        /// </summary>
        public Dictionary<ulong, LastCharacterCall> Conversations { get; }


        public DiscordWebhookClient? GetWebhookClient(ulong webhookId, string webhookToken);

        public Task<bool> UserIsBanned(SocketCommandContext context);
        public Task<bool> UserIsBanned(SocketReaction reaction, IDiscordClient client);
        public Task<string?> UpdateGuildAisekaiAuthTokenAsync(ulong guildId, string refreshToken);

        public void Initialize();
        public void WatchDogClear();

    }
}
