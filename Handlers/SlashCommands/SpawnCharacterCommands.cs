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
using CharacterEngineDiscord.Services.AisekaiIntegration.SearchEnums;
using System.ComponentModel;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireManagerAccess]
    [Group("spawn", "Spawn new character")]
    public class SpawnCharacterCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        const string sqDesc = "When use character ID, set 'set-with-id' to 'True'";
        const string tagsDesc = "Tags separated by ','";
        
        public SpawnCharacterCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
        }


        [SlashCommand("cai-character", "Add new CharacterAI character to this channel")]
        public async Task SpawnCaiCharacter([Summary(description: sqDesc)] string searchQueryOrCharacterId, bool setWithId = false, bool silent = false)
            => await SpawnCaiCharacterAsync(searchQueryOrCharacterId, setWithId, silent);


        [SlashCommand("aisekai-character", "Add new Aisekai character to this channel")]
        public async Task SpawnAisekaiCharacter([Summary(description: sqDesc)] string? searchQueryOrCharacterId = null, bool setWithId = false, [Summary(description: tagsDesc)] string? tags = null, bool allowNsfw = true, SearchSort sort = SearchSort.desc, SearchTime time = SearchTime.all, SearchType type = SearchType.best, bool silent = false)
            => await SpawnAisekaiCharacterAsync(searchQueryOrCharacterId, setWithId, tags, allowNsfw, sort, time, type, silent);

        [SlashCommand("chub-character", "Add new character from CharacterHub to this channel")]
        public async Task SpawnChubCharacter(ApiTypeForChub apiType, [Summary(description: sqDesc)] string? searchQueryOrCharacterId = null, bool setWithId = false, [Summary(description: tagsDesc)] string? tags = null, bool allowNSFW = true, SortField sortBy = SortField.MostPopular, bool silent = false)
            => await SpawnChubCharacterAsync(apiType, searchQueryOrCharacterId, tags, allowNSFW, sortBy, setWithId, silent);

        [SlashCommand("custom-character", "Add a new character to this channel with full customization")]
        public async Task SpawnCustomTavernCharacter()
        {
            await RespondWithCustomCharModalasync();
        }


        ////////////////////
        //// Long stuff ////
        ////////////////////

        private async Task SpawnChubCharacterAsync(ApiTypeForChub apiType, string? searchQueryOrCharacterId, string? tags, bool allowNSFW, SortField sortBy, bool setWithId, bool silent)
        {
            try
            {   if (Context.Channel is ITextChannel tc && !tc.IsNsfw)
                {
                    await FollowupAsync(embed: "Channel must be marked as NSFW for this command to work".ToInlineEmbed(Color.Purple), ephemeral: silent);
                    return;
                }
            }
            catch (NullReferenceException)
            {
                throw new($"{WARN_SIGN_DISCORD} You have to invite the bot to this channel to execute its commands here!");
            }

            await DeferAsync(ephemeral: silent);

            IntegrationType integrationType = apiType is ApiTypeForChub.OpenAI ? IntegrationType.OpenAI
                                            : apiType is ApiTypeForChub.KoboldAI ? IntegrationType.KoboldAI
                                            : apiType is ApiTypeForChub.HordeKoboldAI ? IntegrationType.HordeKoboldAI
                                            : IntegrationType.Empty;

            if (setWithId)
            {
                await FollowupAsync(embed: WAIT_MESSAGE, ephemeral: silent);

                var chubCharacter = await GetChubCharacterInfo(searchQueryOrCharacterId ?? string.Empty, _integration.CommonHttpClient);
                var character = CharacterFromChubCharacterInfo(chubCharacter);                 
                await FinishSpawningAsync(integrationType, character);
            }
            else // set with search
            {
                await FollowupAsync(embed: WAIT_MESSAGE, ephemeral: silent);

                var response = await SearchChubCharactersAsync(new()
                {
                    Text = searchQueryOrCharacterId ?? "",
                    Amount = 300,
                    Tags = tags ?? "",
                    ExcludeTags = "",
                    Page = 1,
                    SortBy = sortBy,
                    AllowNSFW = allowNSFW
                }, _integration.CommonHttpClient);

                var searchQueryData = SearchQueryDataFromChubResponse(integrationType, response);
                await FinishSearchAsync(searchQueryData);
            }
        }

        private async Task SpawnCaiCharacterAsync(string searchQueryOrCharacterId, bool setWithId = false, bool silent = false)
        {
            try { _ = (Context.Channel is ITextChannel tc && !tc.IsNsfw); }
            catch (NullReferenceException)
            {
                throw new($"{WARN_SIGN_DISCORD} You have to invite the bot to this channel to execute its commands here!");
            }

            await DeferAsync(ephemeral: silent);

            if (_integration.CaiClient is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} CharacterAI integration is disabled".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
            var caiToken = channel.Guild.GuildCaiUserToken ?? string.Empty;

            if (string.IsNullOrWhiteSpace(caiToken))
            {
                await FollowupAsync(embed: ($"{WARN_SIGN_DISCORD} You have to specify a CharacterAI auth token for your server first!\n" +
                                            $"How to get CharacterAI auth token: [wiki/Important-Notes-and-Additional-Guides](https://github.com/drizzle-mizzle/Character-Engine-Discord/wiki/Important-Notes-and-Additional-Guides#get-characterai-user-auth-token)\n" +
                                            $"Command: `/set-server-cai-token`").ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            var plusMode = channel.Guild.GuildCaiPlusMode ?? false;

            await FollowupAsync(embed: WAIT_MESSAGE, ephemeral: silent);

            if (setWithId)
            {
                var caiCharacter = await _integration.CaiClient.GetInfoAsync(searchQueryOrCharacterId, customAuthToken: caiToken, customPlusMode: plusMode);
                var character = CharacterFromCaiCharacterInfo(caiCharacter);

                await FinishSpawningAsync(IntegrationType.CharacterAI, character);
            }
            else // set with search
            {
                var response = await _integration.CaiClient.SearchAsync(searchQueryOrCharacterId, customAuthToken: caiToken, customPlusMode: plusMode);
                var searchQueryData = SearchQueryDataFromCaiResponse(response);

                await FinishSearchAsync(searchQueryData);
            }
        }

        private async Task SpawnAisekaiCharacterAsync(string? searchQueryOrCharacterId, bool setWithId, string? tags, bool nsfw, SearchSort sort, SearchTime time, SearchType type, bool silent)
        {
            try
            {   if (Context.Channel is ITextChannel tc && !tc.IsNsfw)
                {
                    await RespondAsync(embed: "Channel must be marked as NSFW for this command to work".ToInlineEmbed(Color.Purple), ephemeral: silent);
                    return;
                }
            }
            catch (NullReferenceException)
            {
                throw new($"{WARN_SIGN_DISCORD} You have to invite the bot to this channel to execute its commands here!");
            }

            await DeferAsync(ephemeral: silent);

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
            string? authToken = channel.Guild.GuildAisekaiAuthToken;

            if (string.IsNullOrWhiteSpace(authToken))
            {
                await FollowupAsync(embed: ($"{WARN_SIGN_DISCORD} You have to specify an Aisekai user account for your server first!\n" +                                            
                                            $"Command: `/set-server-aisekai-auth email:... password:...`").ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await FollowupAsync(embed: WAIT_MESSAGE, ephemeral: silent);

            if (setWithId)
            {
                await SpawnAisekaiCharacterWithIdAsync(channel, searchQueryOrCharacterId ?? string.Empty, authToken);
            }
            else // set with search
            {
                var response = await _integration.AisekaiClient.GetSearchAsync(authToken, searchQueryOrCharacterId, time, type, sort, nsfw, 1, 100, tags);
                var searchQueryData = SearchQueryDataFromAisekaiResponse(response);

                await FinishSearchAsync(searchQueryData);
            }
        }

        private async Task SpawnAisekaiCharacterWithIdAsync(Models.Database.Channel channel, string characterId, string authToken)
        {
            var response = await _integration.AisekaiClient.GetCharacterInfoAsync(authToken, characterId);

            if (response.IsSuccessful)
            {
                var character = CharacterFromAisekaiCharacterInfo(response.Character!.Value);
                await FinishSpawningAsync(IntegrationType.Aisekai, character);
            }
            else if (response.Code == 401)
            {   // Re-login
                var newAuthToken = await _integration.UpdateGuildAisekaiAuthTokenAsync(channel.GuildId, channel.Guild.GuildAisekaiRefreshToken!);
                if (newAuthToken is null)
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to authorize Aisekai account`".ToInlineEmbed(Color.Red));
                else
                    await SpawnAisekaiCharacterWithIdAsync(channel, characterId, newAuthToken);
            }
            else
            {
                await ModifyOriginalResponseAsync(r => r.Embed = $"{WARN_SIGN_DISCORD} Failed to get character info: `{response.ErrorReason}`".ToInlineEmbed(Color.Red));
            }
        }

        private async Task FinishSpawningAsync(IntegrationType type, Models.Database.Character? character)
        {
            if (character is null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Embed = $"{WARN_SIGN_DISCORD} Something went wrong!".ToInlineEmbed(Color.Red));
                return;
            }

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
            var fromChub = type is not IntegrationType.CharacterAI && type is not IntegrationType.Aisekai;
            var characterWebhook = await _integration.CreateCharacterWebhookAsync(type, Context, character, _integration, fromChub);

            if (characterWebhook is null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Embed = $"{WARN_SIGN_DISCORD} Something went wrong!".ToInlineEmbed(Color.Red));
                return;
            }

            var webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            _integration.WebhookClients.TryAdd(characterWebhook.Id, webhookClient);

            var originalMessage = await ModifyOriginalResponseAsync(msg => msg.Embed = SpawnCharacterEmbed(characterWebhook));
            if (type is IntegrationType.Aisekai)
                await Context.Channel.SendMessageAsync(embed: ":zap: Please, pay attention to the fact that Aisekai characters don't support separate chat histories. Thus, if you will spawn the same character in two different channels, both channels will continue to share the same chat context; same goes for `/reset-character` command — once it's executed, the chat history will be deleted in each channel where specified character is present.".ToInlineEmbed(Color.Gold, false));

            string characterMessage = $"{Context.User.Mention} {character.Greeting.Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{(Context.User as SocketGuildUser)?.GetBestName()}**")}";
            if (characterMessage.Length > 2000) characterMessage = characterMessage[0..1994] + "[...]";

            // Try to set avatar
            Stream? image = null;
            if (!string.IsNullOrWhiteSpace(characterWebhook.Character.AvatarUrl))
            {
                var imageUrl = originalMessage.Embeds?.Single()?.Image?.ProxyUrl;
                image = await TryToDownloadImageAsync(imageUrl, _integration.ImagesHttpClient);
            }
            image ??= new MemoryStream(File.ReadAllBytes($"{EXE_DIR}{SC}storage{SC}default_avatar.png"));
            await webhookClient.ModifyWebhookAsync(w => w.Image = new Image(image));

            await webhookClient.SendMessageAsync(characterMessage);
        }

        private async Task FinishSearchAsync(SearchQueryData searchQueryData)
        {
            var newSQ = await BuildAndSendSelectionMenuAsync(Context, searchQueryData);
            if (newSQ is null) return;

            var lastSQ = _integration.SearchQueries.Find(sq => sq.ChannelId == newSQ.ChannelId);

            await _integration.SearchQueriesLock.WaitAsync();
            try
            {
                if (lastSQ is not null) // stop tracking the last query in this channel
                    _integration.SearchQueries.Remove(lastSQ);

                _integration.SearchQueries.Add(newSQ); // and start tracking this one
            }
            finally
            {
                _integration.SearchQueriesLock.Release();
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
                                            .AddTextInput($"Avatar url", "avatar-url", TextInputStyle.Short, "https://some.site/.../avatar.jpg", required: false)
                                            .Build();

            await RespondWithModalAsync(modal);
        }
    }
}
