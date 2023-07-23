using Discord;
using Discord.Webhook;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.CharacterHub;
using CharacterEngineDiscord.Models.Database;
using System;
using System.Threading.Channels;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    public class PerChannelCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly StorageContext _db;

        public enum TavernApiType
        {
            OpenAI
        }

        public PerChannelCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("update-character", "Change character settings")]
        public async Task UpdateCharacter(ulong webhookId, string newCallPrefix, [Summary(description: "Add a following spacebar for the prefix, e.g. `..prefix `")] bool addFollowingSpacebar)
        {
            try { await UpdateCharacterAsync(webhookId, newCallPrefix, addFollowingSpacebar); }
            catch (Exception e ) { LogException(new[] { e }); }
        }


        [SlashCommand("spawn-cai-character", "Add a new character from CharacterAI to this channel")]
        public async Task SpawnCaiCharacter([Summary(description: "When specify a character ID, set 'set-with-id' parameter to 'True'")] string searchQueryOrCharacterId, bool setWithId = false)
        {
            try { await SpawnCaiCharacterAsync(searchQueryOrCharacterId, setWithId); }
            catch (Exception e) { LogException(new[] { e }); }

        }

        const string sqDesc = "When specify it with a character ID, set 'set-with-id' parameter to 'True'";
        const string tagsDesc = "Tags separated by ','";

        [SlashCommand("spawn-tavern-character", "Add a new character from CharacterHub to this channel")]
        public async Task SpawnChubCharacter([Summary(description: sqDesc)] string searchQueryOrCharacterId, TavernApiType apiType, [Summary(description: tagsDesc)] string? tags = null, bool allowNSFW = true, bool setWithId = false)
        {
            try { await SpawnChubCharacterAsync(searchQueryOrCharacterId, apiType, tags, allowNSFW, setWithId); }
            catch (Exception e) { LogException(new[] { e }); }

        }

        [SlashCommand("spawn-custom-tavern-character", "Add a new character to this channel with full customization")]
        public async Task SpawnCustomTavernCharacter()
        {
            try { }
            catch (Exception e) { LogException(new[] { e }); }
        }


        [SlashCommand("show-characters", "Show all characters in this channel")]
        public async Task ShowCharacters()
        {
            try { await ShowCharactersAsync(); }
            catch (Exception e) { LogException(new[] { e }); }

        }


        ////////////////////////////
        //// Main logic section ////
        ////////////////////////////

        private async Task ShowCharactersAsync()
        {
            await DeferAsync();

            var channel = await _db.Channels.FindAsync(Context.Interaction.ChannelId);
            if (channel is null || channel.CharacterWebhooks.Count == 0)
            {
                await FollowupAsync(embed: InlineEmbed($"{OK_SIGN_DISCORD} No characters were found in this channel", Color.Orange));
                return;
            }

            int count = 0;
            string result = "";
            foreach (var characterWebhook in channel.CharacterWebhooks)
            {
                var integrationType = characterWebhook.IntegrationType is IntegrationType.CharacterAI ? "c.ai" :
                                      characterWebhook.IntegrationType is IntegrationType.OpenAI ? characterWebhook.OpenAiModel : "";
                result += $"{count++}. **{characterWebhook.Character.Name}** | *`{characterWebhook.CallPrefix}`* | `{characterWebhook.Id}` | `{integrationType}` \n";
            }

            await FollowupAsync(embed: new EmbedBuilder() { Title = $"{OK_SIGN_DISCORD} {count} character(s) in this channel:", Description = result, Color = Color.Green }.Build());
        }


        private async Task SpawnChubCharacterAsync(string searchQueryOrCharacterId, TavernApiType apiType, string? tags = null, bool allowNSFW = true, bool setWithId = false)
        {
            var guild = await FindOrStartTrackingGuildAsync((ulong)Context.Interaction.GuildId!, _db);
            await DeferAsync();
            switch (apiType)
            {
                case TavernApiType.OpenAI:
                    string? token = guild.GuildOpenAiApiToken ?? ConfigFile.DefaultOpenAiApiToken.Value;
                    if (!string.IsNullOrWhiteSpace(token)) break;

                    await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} You have to specify an OpenAI API token for your server first!", Color.Red));
                    return;
                default: return;
            }

            // It will be extended, don't laugh T_T
            IntegrationType integrationType = apiType is TavernApiType.OpenAI ? IntegrationType.OpenAI : IntegrationType.OpenAI;

            if (setWithId)
            {
                await FollowupAsync(embed: InlineEmbed(WAIT_MESSAGE, Color.Teal));
                
                var chubCharacter = await GetChubCharacterInfo(searchQueryOrCharacterId, _integration.HttpClient);
                var character = CharacterFromChubCharacterInfo(chubCharacter);
                await FinishSpawningAsync(integrationType, character);
            }
            else // set with search
            {
                await FollowupAsync(embed: InlineEmbed(WAIT_MESSAGE, Color.Teal));
                
                var response = await SearchChubCharactersAsync(new()
                {
                    Text = searchQueryOrCharacterId,
                    Tags = string.Join(',', tags),
                    AllowNSFW = allowNSFW,
                    SortBy = SortField.MostPopular
                }, _integration.HttpClient);

                var searchQueryData = SearchQueryDataFromChubResponse(response);
                await FinishSearchAsync(searchQueryData);
            }
        }

        private async Task SpawnCaiCharacterAsync([Summary(description: "When specify a character ID, set 'set-with-id' parameter to 'True'")] string searchQueryOrCharacterId, bool setWithId = false)
        {
            await DeferAsync();

            if (_integration.CaiClient is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} CharacterAI integration is disabled", Color.Red));
                return;
            }

            var guild = await FindOrStartTrackingGuildAsync((ulong)Context.Interaction.GuildId!, _db);
            if (guild is null) return;

            var caiToken = guild.DefaultCaiUserToken ?? ConfigFile.CaiDefaultUserAuthToken.Value;
            if (string.IsNullOrWhiteSpace(caiToken))
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} You have to specify a CharacterAI auth token for your server first!", Color.Red));
                return;
            }

            var plusMode = guild.DefaultCaiPlusMode ?? ConfigFile.CaiDefaultPlusModeEnabled.Value.ToBool();

            if (setWithId)
            {
                await FollowupAsync(embed: InlineEmbed(WAIT_MESSAGE, Color.Teal));

                var caiCharacter = await _integration.CaiClient.GetInfoAsync(searchQueryOrCharacterId, customAuthToken: caiToken, customPlusMode: plusMode);
                var character = CharacterFromCaiCharacterInfo(caiCharacter);

                await FinishSpawningAsync(IntegrationType.CharacterAI, character);
            }
            else // set with search
            {
                await FollowupAsync(embed: InlineEmbed(WAIT_MESSAGE, Color.Teal));
                
                var response = await _integration.CaiClient.SearchAsync(searchQueryOrCharacterId, customAuthToken: caiToken, customPlusMode: plusMode);
                var searchQueryData = SearchQueryDataFromCaiResponse(response);

                await FinishSearchAsync(searchQueryData);
            }
        }

        private async Task UpdateCharacterAsync(ulong webhookId, string newCallPrefix, [Summary(description: "Add a following spacebar for the prefix, e.g. `..prefix `")] bool addFollowingSpacebar)
        {
            await DeferAsync();

            var characterWebhook = await _db.CharacterWebhooks.FindAsync(webhookId);
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook with the given ID was not found.", Color.Red));
                return;
            }

            characterWebhook.CallPrefix = newCallPrefix + (addFollowingSpacebar ? " " : "");
            await _db.SaveChangesAsync();
            await FollowupAsync(embed: SuccessEmbed());
        }


        // Sub-logic

        private async Task FinishSpawningAsync(IntegrationType type, Models.Database.Character? character)
        {
            if (character is null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Embed = FailedToSetCharacterEmbed());
                return;
            }

            var channel = await FindOrStartTrackingChannelAsync((ulong)Context.Interaction.ChannelId!, (ulong)Context.Interaction.GuildId!, _db);

            var characterWebhook = await CreateCharacterWebhookAsync(type, Context, character, _db, _integration);
            if (characterWebhook is null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Embed = InlineEmbed($"{WARN_SIGN_DISCORD} Something went wrong!", Color.Red));
                return;
            }

            var webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            _integration.WebhookClients.Add(characterWebhook.Id, webhookClient);

            await ModifyOriginalResponseAsync(msg => msg.Embed = SpawnCharacterEmbed(characterWebhook, character));
            await webhookClient.SendMessageAsync($"{Context.User.Mention} {character.Greeting}");
        }

        private async Task FinishSearchAsync(SearchQueryData searchQueryData)
        {
            var query = await BuildAndSendSelectionMenuAsync(Context, searchQueryData);
            if (query is null) return;

            // Stop tracking last query in this channel
            var lastSQ = _integration.SearchQueries.Find(sq => sq.ChannelId == Context.Interaction.ChannelId);
            if (lastSQ is not null) _integration.SearchQueries.Remove(lastSQ);

            _integration.SearchQueries.Add(query);
        }

    }
}
