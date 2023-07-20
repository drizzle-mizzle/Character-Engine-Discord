using Discord;
using Discord.Webhook;
using Discord.Interactions;
using CharacterAI;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Database;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.StorageContext;
using Discord.WebSocket;
using CharacterAI.Models;

namespace CharacterEngineDiscord.Services
{
    public class IntegrationsService
    {
        internal HttpClient @HttpClient { get; } = new();
        internal CharacterAIClient? CaiClient { get; set; }
        internal List<SearchQuery> SearchQueries { get; set; } = new();
        internal Dictionary<ulong, DiscordWebhookClient> WebhookClients { get; set; } = new();

        private readonly Dictionary<ulong, int[]> _watchDog = new();

        public async Task Initialize()
        {
            string? envToken = Environment.GetEnvironmentVariable("CAI_TOKEN");
            Environment.SetEnvironmentVariable("CAI_TOKEN", null);

            bool useCai = bool.Parse(ConfigFile.UseCAI.Value!);
            if (useCai)
            {
                LogYellow("\n[using CharacterAI]\n");
                CaiClient = new(
                    userToken: envToken ?? ConfigFile.CAIuserAuthToken.Value!,
                    caiPlusMode: bool.Parse(ConfigFile.UseCAIplusMode.Value ?? "false"),
                    browserType: ConfigFile.PuppeteerBrowserType.Value,
                    customBrowserDirectory: ConfigFile.PuppeteerBrowserDir.Value,
                    customBrowserExecutablePath: ConfigFile.PuppeteerBrowserExe.Value
                );
                await CaiClient.LaunchBrowserAsync(killDuplicates: true);
                AppDomain.CurrentDomain.ProcessExit += (s, args) => CaiClient.KillBrowser();
                Log("CharacterAI - "); LogGreen("Ready\n\n");
            }
        }

        public enum IntegrationType
        {
            CharacterAI
        }

        internal static async Task<CharacterWebhook?> CreateChannelCharacterWebhookAsync(IntegrationType type, InteractionContext context, Models.Database.Character character, StorageContext db, IntegrationsService integration)
        {
            if (integration.CaiClient is null) return null;

            string callPrefix = $"..{character.Name![..2].ToLower()} ";
            var discordChannel = (IIntegrationChannel)(await context.Interaction.GetOriginalResponseAsync()).Channel;
           
            var image = await TryDownloadImgAsync(character.AvatarUrl, integration.HttpClient);
            var channelWebhook = await discordChannel.CreateWebhookAsync(character.Name, image);
            if (channelWebhook is null) return null;

            var caiHistoryId = await integration.CaiClient.CreateNewChatAsync(character.Id);
            if (caiHistoryId is null) return null;

            var webhook = db.CharacterWebhooks.Add(new CharacterWebhook()
            {
                Id = channelWebhook.Id,
                WebhookToken = channelWebhook.Token,
                Channel = await FindOrStartTrackingChannelAsync((ulong)context.Interaction.ChannelId!, context.Guild.Id, db),
                Character = character,
                IntegrationType = type,
                CallPrefix = callPrefix,
                ActiveHistoryId = caiHistoryId,
                MessagesFormat = "{{msg}}",
                ReplyChance = 0,
                ReplyDelay = 3,
                TranslateLanguage = "ru"
            }).Entity;
            await db.SaveChangesAsync();
            
            return webhook;
        }

        public static SearchQueryData SearchQueryDataFromCaiResponse(SearchResponse response)
        {
            var characters = new List<Models.Database.Character>();
            foreach (var c in response.Characters)
            {
                var cc = CharacterFromCaiCharacterInfo(c);
                if (cc is null) continue;

                characters.Add(cc);
            }

            return new(characters, response.OriginalQuery) { ErrorReason = response.ErrorReason };
        }

        public static Models.Database.Character? CharacterFromCaiCharacterInfo(CharacterAI.Models.Character caiCharacter)
        {
            if (caiCharacter.IsEmpty) return null;

            return new()
            {
                Id = caiCharacter.Id!,
                Tgt = caiCharacter.Tgt!,
                Name = caiCharacter.Name!,
                Title = caiCharacter.Title,
                Greeting = caiCharacter.Greeting,
                Description = caiCharacter.Description,
                AuthorName = caiCharacter.Author,
                Link = $"https://beta.character.ai/chat?char={caiCharacter.Id}",
                AvatarUrl = caiCharacter.AvatarUrlFull ?? caiCharacter.AvatarUrlMini,
                ImageGenEnabled = caiCharacter.ImageGenEnabled ?? false,
                Interactions = caiCharacter.Interactions ?? 0
            };
        }

        internal async Task<bool> UserIsBanned(SocketUserMessage message, StorageContext db, bool checkOnly = false)
        {
            ulong currUserId = message.Author.Id;
            var iu = await db.IgnoredUsers.FindAsync(currUserId);

            if (iu is not null) return true;
            if (checkOnly) return false;

            int currMinute = message.CreatedAt.Minute + message.CreatedAt.Hour * 60;

            // Start watching for user
            if (!_watchDog.ContainsKey(currUserId))
                _watchDog.Add(currUserId, new int[] { -1, 0 }); // current minute : count

            // Drop + update user stats if he replies in new minute
            if (_watchDog[currUserId][0] != currMinute)
            {
                _watchDog[currUserId][0] = currMinute;
                _watchDog[currUserId][1] = 0;
            }

            // Update messages count withing current minute
            _watchDog[currUserId][1]++;

            int rateLimit = int.Parse(ConfigFile.RateLimit.Value!);

            if (_watchDog[currUserId][1] == rateLimit - 1)
                await message.ReplyAsync($"{WARN_SIGN_DISCORD} Warning! If you proceed to call the bot so fast, you'll be blocked from using it.");
            else if (_watchDog[currUserId][1] > rateLimit)
            {
                await db.IgnoredUsers.AddAsync(new() { Id = currUserId });
                await db.SaveChangesAsync();
                _watchDog.Remove(currUserId);

                return true;
            }

            return false;
        }

        internal static async Task<bool> UserIsBanned(IUser user, StorageContext db)
            => (await db.IgnoredUsers.FindAsync(user.Id)) is not null;

        internal static Embed InlineEmbed(string text, Color color)
            => new EmbedBuilder() { Description = $"**{text}**", Color = color }.Build();

        internal static Embed FailedToSetCharacterEmbed()
            => InlineEmbed($"{WARN_SIGN_DISCORD} Failed to set a character", Color.Red);

        internal static Embed SuccessMsg()
            => InlineEmbed($"{OK_SIGN_DISCORD} Success", Color.Green);

    }
}
