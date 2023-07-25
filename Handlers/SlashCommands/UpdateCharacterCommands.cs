using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using Discord.WebSocket;
using static CharacterEngineDiscord.Handlers.SlashCommands.SpawnCharacterCommands;
using static CharacterEngineDiscord.Handlers.SlashCommands.PerServerCommands;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireManagerAccess]
    [Group("update-character", "Change character settings")]
    public class UpdateCharacterCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly StorageContext _db;

        public UpdateCharacterCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("call-prefix", "Change character call prefix")]
        public async Task CallPrefix(string webhookId, string newCallPrefix, [Summary(description: "Add a following spacebar for the prefix, e.g. `..prefix `")] bool addFollowingSpacebar)
        {
            try { await UpdatePrefixAsync(webhookId, newCallPrefix, addFollowingSpacebar); }
            catch (Exception e) { LogException(new[] { e }); }   
        }

        [SlashCommand("api", "Change API backend")]
        public async Task ApiBackend(string webhookId, ApiType apiType, OpenAiModel? openAiModel = null, string? personalToken = null)
        {
            try { await UpdateApiAsync(webhookId, apiType, openAiModel, personalToken); }
            catch (Exception e) { LogException(new[] { e }); }
        }

        [SlashCommand("quotes", "Enable/disable qoutes")]
        public async Task Quotes(string webhookId, bool state)
        {
            
            try { await UpdateQuotesAsync(webhookId, state); }
            catch (Exception e) { LogException(new[] { e }); }
        }

        [SlashCommand("messages-format", "Change character messages format")]
        public async Task MessagesFormat(string webhookId, string newFormat)
        {
            try { await UpdateMessagesFormatAsync(webhookId, newFormat); }
            catch (Exception e) { LogException(new[] { e }); }
        }

        [SlashCommand("jailbreak-prompt", "Change character jailbreak prompt")]
        public async Task JailbreakPrompt(string webhookId)
        {
            var modal = new ModalBuilder().WithTitle($"Update jailbreak prompt for the character")
                                          .WithCustomId($"upd~{webhookId}")
                                          .AddTextInput("New jailbreak prompt", "new-prompt", TextInputStyle.Paragraph)
                                          .Build();
            await RespondWithModalAsync(modal);
        }

        ////////////////////////////
        //// Main logic section ////
        ////////////////////////////

        private async Task UpdatePrefixAsync(string webhookId, string newCallPrefix, [Summary(description: "Add a following spacebar for the prefix, e.g. `..prefix `")] bool addFollowingSpacebar)
        {
            await DeferAsync();

            var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            characterWebhook.CallPrefix = newCallPrefix + (addFollowingSpacebar ? " " : "");
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        private async Task UpdateApiAsync(string webhookId, ApiType apiType, OpenAiModel? openAiModel = null, string? personalApiToken = null)
        {
            await DeferAsync();

            var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }
            
            if (apiType is ApiType.OpenAI)
            {
                var model = openAiModel is OpenAiModel.GPT_3_5_turbo ? "gpt-3.5-turbo" : openAiModel is OpenAiModel.GPT_4 ? "gpt-4" : null;
                if (model is null)
                {
                    await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Specify an OpenAI model!", Color.Red));
                    return;
                }

                characterWebhook.IntegrationType = IntegrationType.CharacterAI;
                characterWebhook.OpenAiModel = model;
                characterWebhook.PersonalOpenAiApiToken = personalApiToken;
            }
            
            await _db.SaveChangesAsync();
            await FollowupAsync(embed: SuccessEmbed());
        }

        private async Task UpdateQuotesAsync(string webhookId, bool state)
        {
            await DeferAsync();

            var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            characterWebhook.ReferencesEnabled = state;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        private async Task UpdateMessagesFormatAsync(string webhookId, string newFormat)
        {
            await DeferAsync();

            var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            if (!newFormat.Contains("{{msg}}"))
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Can't set format without **`{{{{msg}}}}`** placeholder!", Color.Red));
                return;
            }

            characterWebhook.MessagesFormat = newFormat;
            await _db.SaveChangesAsync();

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **{characterWebhook.Character.Name}**")
                                          .AddField("New format:", $"`{characterWebhook.MessagesFormat}`")
                                          .AddField("[Example]", $"User message: *`Hello!`*\n" +
                                                                 $"User nickname: `Average AI Enjoyer`\n" +
                                                                 $"Result (what character will see): *`{characterWebhook.MessagesFormat.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer")}`*")
                                          .WithColor(Color.Green)
                                          .Build();

            await FollowupAsync(embed: embed);
        }

    }
}
