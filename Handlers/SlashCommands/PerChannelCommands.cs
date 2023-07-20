using Discord;
using Discord.Webhook;
using Discord.Interactions;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    public class PerChannelCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly InteractionService _interactions;
        private readonly StorageContext _db;

        public PerChannelCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _interactions = services.GetRequiredService<InteractionService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("update-character", "Change character settings")]
        public async Task UpdateCharacterAsync(ulong webhookId, string newCallPrefix, bool addFollowingSpacebar)
        {
            var characterWebhook = await _db.CharacterWebhooks.FindAsync(webhookId);
            if (characterWebhook is null)
            {
                await RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook with the given ID was not found.", Color.Red));
                return;
            }

            characterWebhook.CallPrefix = newCallPrefix + (addFollowingSpacebar ? " " : "");
            await _db.SaveChangesAsync();
            await RespondAsync(embed: SuccessMsg());
        }

        [SlashCommand("spawn-character", "Add a new character to this channel")]
        public async Task SpawnCharacterAsync(IntegrationType type, string searchQueryOrCharacterId, bool setWithId = false)
        {
            switch (type)
            {
                case IntegrationType.CharacterAI:
                    await TryToSpawnCaiCharacterAsync(searchQueryOrCharacterId, setWithId);
                    break;
                default:
                    return;
            }
        }

        [SlashCommand("show-characters", "Show all characters in this channel")]
        public async Task ShowCharactersAsync()
        {
            var channel = await _db.Channels.FindAsync(Context.Interaction.ChannelId);
            if (channel is null || channel.CharacterWebhooks.Count == 0)
            {
                await RespondAsync(embed: InlineEmbed($"{OK_SIGN_DISCORD} No characters were found in this channel", Color.Orange));
                return;
            }

            await DeferAsync();

            int count = 0;
            string result = "";
            foreach (var characterWebhook in channel.CharacterWebhooks)
                result += $"{count++}. {characterWebhook.Character.Name} | *`{characterWebhook.CallPrefix}`* | *`{characterWebhook.Id}`*\n";

            await FollowupAsync(embed: new EmbedBuilder() { Title = $"{OK_SIGN_DISCORD} {count} character(s) in this channel:", Description = result }.Build());
        }

        private async Task TryToSpawnCaiCharacterAsync(string searchQueryOrCharacterId, bool setWithId)
        {
            if (_integration.CaiClient is null)
            {
                await RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} CharacterAI integration is disabled", Color.Red));
                return;
            }

            else if (setWithId)
            {
                await RespondAsync(embed: InlineEmbed(WAIT_MESSAGE, Color.Teal));
                try
                {
                    var caiCharacter = await _integration.CaiClient.GetInfoAsync(searchQueryOrCharacterId);
                    var character = CharacterFromCaiCharacterInfo(caiCharacter);
                    await FinishSpawningAsync(IntegrationType.CharacterAI, character);
                }
                catch (Exception e) { LogException(new[] { e }); }
            }
            else // set with search
            {
                await RespondAsync(embed: InlineEmbed(WAIT_MESSAGE, Color.Teal));
                try
                {
                    var response = await _integration.CaiClient.SearchAsync(searchQueryOrCharacterId);
                    var searchQueryData = SearchQueryDataFromCaiResponse(response);

                    var query = await BuildAndSendSelectionMenuAsync(Context, searchQueryData);
                    if (query is null) return;

                    // Stop tracking last query in this channel
                    var lastSQ = _integration.SearchQueries.Find(sq => sq.ChannelId == Context.Interaction.ChannelId);
                    if (lastSQ is not null) _integration.SearchQueries.Remove(lastSQ);

                    _integration.SearchQueries.Add(query);
                }
                catch (Exception e) { LogException(new[] { e }); }
            }
        }

        private async Task FinishSpawningAsync(IntegrationType type, Models.Database.Character? character)
        {
            if (character is null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Embed = FailedToSetCharacterEmbed());
                return;
            }

            var channel = await FindOrStartTrackingChannelAsync((ulong)Context.Interaction.ChannelId!, (ulong)Context.Interaction.GuildId!, _db);
            if (channel.CharacterWebhooks.Count == 20)
            {
                await ModifyOriginalResponseAsync(msg => msg.Embed = InlineEmbed($"{WARN_SIGN_DISCORD} You can't add more than **20** characters in one channel", Color.Orange));
                return;
            }

            var webhook = await CreateChannelCharacterWebhookAsync(type, Context, character, _db, _integration);
            if (webhook is null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Embed = InlineEmbed($"{WARN_SIGN_DISCORD} Something went wrong!", Color.Red));
                return;
            }

            var webhookClient = new DiscordWebhookClient(webhook.Id, webhook.WebhookToken);
            _integration.WebhookClients.Add(webhook.Id, webhookClient);

            await ModifyOriginalResponseAsync(msg => msg.Embed = SpawnCharacterEmbed(webhook, character));
            await webhookClient.SendMessageAsync($"{Context.User.Mention} {character.Greeting}");
        }
    }
}
