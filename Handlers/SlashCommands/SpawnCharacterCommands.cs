using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.CharacterHub;
using Discord.Webhook;
using Discord.WebSocket;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireManagerAccess]
    [Group("spawn", "Spawn new character")]
    public class SpawnCharacterCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        //private readonly DiscordSocketClient _client;
        
        public SpawnCharacterCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            //_client = services.GetRequiredService<DiscordSocketClient>();
        }

        [SlashCommand("cai-character", "Add new character from CharacterAI to this channel")]
        public async Task SpawnCaiCharacter([Summary(description: "When specify a character ID, set 'set-with-id' parameter to 'True'")] string searchQueryOrCharacterId, bool setWithId = false)
        {
            await SpawnCaiCharacterAsync(searchQueryOrCharacterId, setWithId);
        }

        const string sqDesc = "When specify it with a character ID, set 'set-with-id' parameter to 'True'";
        const string tagsDesc = "Tags separated by ','";
        [SlashCommand("chub-character", "Add new character from CharacterHub to this channel")]
        public async Task SpawnChubCharacter(ApiTypeForChub apiType, [Summary(description: sqDesc)] string? searchQueryOrCharacterId = null, [Summary(description: tagsDesc)] string? tags = null, bool allowNSFW = true, SortField sortBy = SortField.MostPopular, bool setWithId = false)
        {
            await SpawnChubCharacterAsync(apiType, searchQueryOrCharacterId, tags, allowNSFW, sortBy, setWithId);
        }

        [SlashCommand("custom-character", "Add new character to this channel with full customization")]
        public async Task SpawnCustomTavernCharacter()
        {
            await RespondWithCustomCharModalasync();
        }


        ////////////////////////////
        //// Main logic section ////
        ////////////////////////////

        private async Task SpawnChubCharacterAsync(ApiTypeForChub apiType, string? searchQueryOrCharacterId, string? tags, bool allowNSFW, SortField sortBy, bool setWithId)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);

            switch (apiType)
            {
                case ApiTypeForChub.OpenAI:
                    string? token = channel.Guild.GuildOpenAiApiToken ?? ConfigFile.DefaultOpenAiApiToken.Value;
                    if (!string.IsNullOrWhiteSpace(token)) break;

                    await FollowupAsync(embed: ($"{WARN_SIGN_DISCORD} You have to specify an OpenAI API token for your server first!\n" +
                                                $"Command: `/set-server-openai-api-token token:YOUR_TOKEN gpt-model:gpt-3.5-turbo`").ToInlineEmbed(Color.Red));
                    return;
                default: return;
            }

            // It will be extended, don't laugh T_T
            IntegrationType integrationType = apiType is ApiTypeForChub.OpenAI ? IntegrationType.OpenAI : IntegrationType.OpenAI;

            if (setWithId)
            {
                await FollowupAsync(embed: WAIT_MESSAGE);

                var chubCharacter = await GetChubCharacterInfo(searchQueryOrCharacterId ?? "", _integration.HttpClient);
                var character = CharacterFromChubCharacterInfo(chubCharacter);
                await FinishSpawningAsync(integrationType, character);
            }
            else // set with search
            {
                await FollowupAsync(embed: WAIT_MESSAGE);

                var response = await SearchChubCharactersAsync(new()
                {
                    Text = searchQueryOrCharacterId ?? "",
                    Amount = 300,
                    Tags = tags ?? "",
                    ExcludeTags = "",
                    Page = 1,
                    SortBy = sortBy,
                    AllowNSFW = allowNSFW
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
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} CharacterAI integration is disabled".ToInlineEmbed(Color.Red));
                return;
            }

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
            var caiToken = channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;

            if (string.IsNullOrWhiteSpace(caiToken))
            {
                await FollowupAsync(embed: ($"{WARN_SIGN_DISCORD} You have to specify a CharacterAI auth token for your server first!\n" +
                                            $"How to get CharacterAI auth token: [wiki/Important-Notes-and-Additional-Guides](https://github.com/drizzle-mizzle/Character-Engine-Discord/wiki/Important-Notes-and-Additional-Guides#get-characterai-user-auth-token)\n" +
                                            $"Command: `/set-server-cai-user-token token:YOUR_TOKEN`").ToInlineEmbed(Color.Red));
                return;
            }

            var plusMode = channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();

            if (setWithId)
            {
                await FollowupAsync(embed: WAIT_MESSAGE);

                var caiCharacter = await _integration.CaiClient.GetInfoAsync(searchQueryOrCharacterId, customAuthToken: caiToken, customPlusMode: plusMode);
                var character = CharacterFromCaiCharacterInfo(caiCharacter);

                await FinishSpawningAsync(IntegrationType.CharacterAI, character);
            }
            else // set with search
            {
                await FollowupAsync(embed: WAIT_MESSAGE);

                var response = await _integration.CaiClient.SearchAsync(searchQueryOrCharacterId, customAuthToken: caiToken, customPlusMode: plusMode);
                var searchQueryData = SearchQueryDataFromCaiResponse(response);

                await FinishSearchAsync(searchQueryData);
            }
        }

        private async Task FinishSpawningAsync(IntegrationType type, Models.Database.Character? character)
        {
            if (character is null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Embed = FailedToSetCharacterEmbed());
                return;
            }

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
            var characterWebhook = await CreateCharacterWebhookAsync(type, Context, character, _integration);

            if (characterWebhook is null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Embed = $"{WARN_SIGN_DISCORD} Something went wrong!".ToInlineEmbed(Color.Red));
                return;
            }

            var webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            _integration.WebhookClients.TryAdd(characterWebhook.Id, webhookClient);

            await ModifyOriginalResponseAsync(msg => msg.Embed = SpawnCharacterEmbed(characterWebhook));

            string characterMessage = $"{Context.User.Mention} {character.Greeting.Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{(Context.User as SocketGuildUser)?.GetBestName()}**")}";
            if (characterMessage.Length > 2000) characterMessage = characterMessage[0..1994] + "[...]";

            await webhookClient.SendMessageAsync(characterMessage);
        }

        private async Task FinishSearchAsync(SearchQueryData searchQueryData)
        {
            var newSQ = await BuildAndSendSelectionMenuAsync(Context, searchQueryData);
            if (newSQ is null) return;

            var lastSQ = _integration.SearchQueries.Find(sq => sq.ChannelId == newSQ.ChannelId);

            lock (_integration.SearchQueries)
            {
                // Stop tracking last query in this channel
                if (lastSQ is not null)
                    _integration.SearchQueries.Remove(lastSQ);

                // Start tracking this one
                _integration.SearchQueries.Add(newSQ);
            }
        }

        private async Task RespondWithCustomCharModalasync()
        {
            var modal = new ModalBuilder().WithTitle($"Create a character")
                                            .WithCustomId("spawn")
                                            .AddTextInput($"Name", "name", TextInputStyle.Short, required: true)
                                            .AddTextInput($"First message", "first-message", TextInputStyle.Paragraph, "*{{char}} joins server*\nHello everyone!", required: true)
                                            .AddTextInput($"Definition-1", "definition-1", TextInputStyle.Paragraph, required: true, value:
                                                        "((DELETE THIS SECTION))\n" +
                                                        "  Discord doesn't allow to set\n" +
                                                        "  more than 5 rows in one modal, so\n" +
                                                        "  you'll have to write the whole\n" +
                                                        "  character definition in these two.\n" +
                                                        "  It's highly recommended to follow\n" +
                                                        "  this exact pattern below and fill\n" +
                                                        "  each line one by one.\n" +
                                                        "  Remove or rename lines that are not\n" +
                                                        "  needed. Custom Jailbreak prompt can\n" +
                                                        "  be set with `/update` command later.\n" +
                                                        "  Default one can be seen be seen with\n" +
                                                        "  `show jailbreak-prompt` command.\n" +
                                                        "((DELETE THIS SECTION))\n\n" +
                                                        "{{char}}'s personality: ALL BASIC INFO ABOUT CHARACTER, CONTINUE IN THE NEXT FIELD IF OUT OF SPACE.")
                                            .AddTextInput($"Definition-2", "definition-2", TextInputStyle.Paragraph, required: false, value:
                                                        "((DELETE THIS SECTION))\n" +
                                                        "  This section will simply continue\n" +
                                                        "  the previous one, as if these two were\n" +
                                                        "  just one big text field.\n" +
                                                        "((DELETE THIS SECTION))\n\n" +
                                                        "Scenario of roleplay: {{char}} has joined Discord!\n\n" +
                                                        "Example conversations between {{char}} and {{user}}:\n<START>\n{{user}}: Nullpo;\n{{char}}: Gah!\n<END>")
                                            .AddTextInput($"Avatar url", "avatar-url", TextInputStyle.Short, "https://avatars.charhub.io/avatars/.../avatar.webp", required: false)
                                            .Build();

            await RespondWithModalAsync(modal);
        }
    }
}
