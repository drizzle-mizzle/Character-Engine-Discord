using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Services.AisekaiIntegration;
using Discord.Commands;
using Discord.Webhook;
using Discord.WebSocket;
using Discord;
using CharacterAI.Client;

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
        public CharacterAiClient? CaiClient { get; set; }
        public List<Guid> RunningCaiTasks { get; }
        public bool CaiReloading { get; set; }

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
        public bool GuildIsAbusive(ulong guildId);

        public Task<string?> UpdateGuildAisekaiAuthTokenAsync(ulong guildId, string refreshToken);

        public void Initialize();
        public Task LaunchCaiAsync();
        public void WatchDogClear();

    }
}
