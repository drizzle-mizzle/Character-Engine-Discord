using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireManagerAccess]
    [Group("update-character", "Change character settings")]
    public class UpdateCharacterCommands : InteractionModuleBase<InteractionContext>
    {
        //private readonly IntegrationsService _integration;
        //private readonly DiscordSocketClient _client;
        private readonly StorageContext _db;

        public UpdateCharacterCommands(IServiceProvider services)
        {
            //_integration = services.GetRequiredService<IntegrationsService>();
            //_client = services.GetRequiredService<DiscordSocketClient>();
            _db = new StorageContext();
        }

        [SlashCommand("call-prefix", "Change character call prefix")]
        public async Task CallPrefix(string webhookIdOrPrefix, string newCallPrefix, [Summary(description: "Add a following spacebar for the prefix, e.g. `..prefix `")] bool addFollowingSpacebar)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());
            characterWebhook ??= channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookIdOrPrefix.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.CallPrefix = newCallPrefix + (addFollowingSpacebar ? " " : "");
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("cai-history-id", "Change c.ai history ID")]
        public async Task CaiHistory(string webhookIdOrPrefix, string newHistoryId)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());
            characterWebhook ??= channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookIdOrPrefix.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.CharacterAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't set history ID for non-CharacterAI integration!".ToInlineEmbed(Color.Red));
                return;
            }

            string message = $"{OK_SIGN_DISCORD} **History ID** for this channel was changed from `{characterWebhook.CaiActiveHistoryId}` to `{newHistoryId}`";
            if (newHistoryId.Length != 43)
                message += $".\nEntered history ID has length that is different from expected ({newHistoryId.Length}/43). Make sure it's correct.";

            characterWebhook.CaiActiveHistoryId = newHistoryId;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: message.ToInlineEmbed(Color.Green));
        }

        [SlashCommand("api", "Change API backend")]
        public async Task ApiBackend(string webhookIdOrPrefix, ApiTypeForChub apiType, OpenAiModel? openAiModel = null, string? personalApiToken = null, string? personalApiEndpoint = null)
        {
            await UpdateApiAsync(webhookIdOrPrefix, apiType, openAiModel, personalApiToken, personalApiEndpoint);
        }

        [SlashCommand("delay", "Set response delay")]
        public async Task Delay(string webhookIdOrPrefix, int seconds)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());
            characterWebhook ??= channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookIdOrPrefix.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.ResponseDelay = seconds;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("name", "Set name")]
        public async Task Name(string webhookIdOrPrefix, string name)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());
            characterWebhook ??= channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookIdOrPrefix.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.Character.Name = name;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("quotes", "Enable/disable quotes")]
        public async Task Quotes(string webhookIdOrPrefix, bool enable)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());
            characterWebhook ??= channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookIdOrPrefix.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.ReferencesEnabled = enable;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("max-tokens", "Change amount of tokens for ChatGPT responses")]
        public async Task MaxTokens(string webhookIdOrPrefix, int tokens)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());
            characterWebhook ??= channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookIdOrPrefix.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.OpenAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Available only for OpenAI integrations!".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.OpenAiMaxTokens = tokens;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("temperature", "Set responses temperature")]
        public async Task Temperature(string webhookIdOrPrefix, float temperature)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());
            characterWebhook ??= channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookIdOrPrefix.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.OpenAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Available only for OpenAI integrations!".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.OpenAiTemperature = temperature;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("presence-penalty", "Set responses presence penalty")]
        public async Task PresPenalty(string webhookIdOrPrefix, float presencePenalty)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());
            characterWebhook ??= channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookIdOrPrefix.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.OpenAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Available only for OpenAI integrations!".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.OpenAiPresencePenalty = presencePenalty;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("frequency-penalty", "Set responses frequency penalty")]
        public async Task FreqPenalty(string webhookIdOrPrefix, float frequencyPenalty)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());
            characterWebhook ??= channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookIdOrPrefix.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.OpenAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Available only for OpenAI integrations!".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.OpenAiFreqPenalty = frequencyPenalty;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("messages-format", "Change character messages format")]
        public async Task MessagesFormat(string webhookIdOrPrefix, string newFormat)
        {
            await UpdateMessagesFormatAsync(webhookIdOrPrefix, newFormat);
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

        private async Task UpdateApiAsync(string webhookIdOrPrefix, ApiTypeForChub apiType, OpenAiModel? openAiModel, string? personalApiToken, string? personalApiEndpont)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());
            characterWebhook ??= channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookIdOrPrefix.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (apiType is ApiTypeForChub.OpenAI)
            {
                var model = openAiModel is OpenAiModel.GPT_3_5_turbo ? "gpt-3.5-turbo" : openAiModel is OpenAiModel.GPT_4 ? "gpt-4" : null;
                if (model is null)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Specify an OpenAI model!".ToInlineEmbed(Color.Red));
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


        private async Task UpdateMessagesFormatAsync(string webhookIdOrPrefix, string newFormat)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());
            characterWebhook ??= channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookIdOrPrefix.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (!newFormat.Contains("{{msg}}"))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't set format without a **`{{{{msg}}}}`** placeholder!".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.MessagesFormat = newFormat;
            await _db.SaveChangesAsync();

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **{characterWebhook.Character.Name}**")
                                          .AddField("New format:", $"`{newFormat}`")
                                          .AddField("[Example]", $"User message: *`Hello!`*\n" +
                                                                 $"User nickname: `Average AI Enjoyer`\n" +
                                                                 $"Referenced message: *`Hola`*\n" +
                                                                 $"Result (what character will see): *`{newFormat.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer").Replace("{{ref_msg_text}}", "Hola")}`*")
                                          .WithColor(Color.Green)
                                          .Build();

            await FollowupAsync(embed: embed);
        }

    }
}
