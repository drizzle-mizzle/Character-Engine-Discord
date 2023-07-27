using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using Discord.WebSocket;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireManagerAccess]
    [Group("update-character", "Change character settings")]
    public class UpdateCharacterCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly DiscordSocketClient _client;
        private readonly StorageContext _db;

        public UpdateCharacterCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("call-prefix", "Change character call prefix")]
        public async Task CallPrefix(string webhookId, string newCallPrefix, [Summary(description: "Add a following spacebar for the prefix, e.g. `..prefix `")] bool addFollowingSpacebar)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            characterWebhook.CallPrefix = newCallPrefix + (addFollowingSpacebar ? " " : "");
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("cai-history-id", "Change c.ai history ID")]
        public async Task CaiHistory(string webhookId, string newHistoryId)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.CharacterAI)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Can't set history ID for non-CharacterAI integration!", Color.Red));
                return;
            }

            string message = $"{OK_SIGN_DISCORD} **History ID** for this channel was changed from `{characterWebhook.CaiActiveHistoryId}` to `{newHistoryId}`";
            if (newHistoryId.Length != 43)
                message += $".\nEntered history ID has length that is different from expected ({newHistoryId.Length}/43). Make sure it's correct.";

            characterWebhook.CaiActiveHistoryId = newHistoryId;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: InlineEmbed(message, Color.Green));
        }

        [SlashCommand("api", "Change API backend")]
        public async Task ApiBackend(string webhookId, ApiTypeForChub apiType, OpenAiModel? openAiModel = null, string? personalApiToken = null, string? personalApiEndpoint = null)
        {
            try { await UpdateApiAsync(webhookId, apiType, openAiModel, personalApiToken, personalApiEndpoint); }
            catch (Exception e) { LogException(new[] { e }); }
        }

        [SlashCommand("quotes", "Enable/disable qoutes")]
        public async Task Quotes(string webhookId, bool state)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            characterWebhook.ReferencesEnabled = state;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("max-tokens", "Change amount of tokens for ChatGPT responses")]
        public async Task MaxTokens(string webhookId, int tokens)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.OpenAI)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Available only for OpenAI integrations!", Color.Red));
                return;
            }

            characterWebhook.OpenAiMaxTokens = tokens;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
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


        ////////////////////
        //// Long stuff ////
        ////////////////////

        private async Task UpdateApiAsync(string webhookId, ApiTypeForChub apiType, OpenAiModel? openAiModel, string? personalApiToken, string? personalApiEndpont)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            if (apiType is ApiTypeForChub.OpenAI)
            {
                var model = openAiModel is OpenAiModel.GPT_3_5_turbo ? "gpt-3.5-turbo" : openAiModel is OpenAiModel.GPT_4 ? "gpt-4" : null;
                if (model is null)
                {
                    await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Specify an OpenAI model!", Color.Red));
                    return;
                }

                characterWebhook.IntegrationType = IntegrationType.OpenAI;
                characterWebhook.OpenAiModel = model;
                if (personalApiToken is not null)
                    characterWebhook.PersonalOpenAiApiToken = personalApiToken;
                if (personalApiEndpont is not null)
                    characterWebhook.PersonalOpenAiApiEndpoint = personalApiEndpont;
            }
            
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }


        private async Task UpdateMessagesFormatAsync(string webhookId, string newFormat)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            if (!newFormat.Contains("{{msg}}"))
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Can't set format without a **`{{{{msg}}}}`** placeholder!", Color.Red));
                return;
            }

            characterWebhook.MessagesFormat = newFormat;
            await _db.SaveChangesAsync();

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **{characterWebhook.Character.Name}**")
                                          .AddField("New format:", $"`{newFormat}`")
                                          .AddField("[Example]", $"User message: *`Hello!`*\n" +
                                                                 $"User nickname: `Average AI Enjoyer`\n" +
                                                                 $"Result (what character will see): *`{newFormat.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer")}`*")
                                          .WithColor(Color.Green)
                                          .Build();

            await FollowupAsync(embed: embed);
        }

    }
}
