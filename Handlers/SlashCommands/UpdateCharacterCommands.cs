using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;
using CharacterEngineDiscord.Models.Database;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireManagerAccess]
    [Group("update", "Change character settings")]
    public class UpdateCharacterCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        //private readonly DiscordSocketClient _client;
        private readonly StorageContext _db;

        public UpdateCharacterCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            //_client = services.GetRequiredService<DiscordSocketClient>();
            _db = new StorageContext();
        }

        [SlashCommand("call-prefix", "Change character call prefix")]
        public async Task SetCallPrefix(string webhookIdOrPrefix, string newCallPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.CallPrefix = newCallPrefix;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("messages-format", "Change character messages format")]
        public async Task SetMessagesFormat(string webhookIdOrPrefix, string newFormat)
        {
            await UpdateMessagesFormatAsync(webhookIdOrPrefix, newFormat);
        }

        [SlashCommand("jailbreak-prompt", "Change character jailbreak prompt")]
        public async Task SetJailbreakPrompt(string webhookIdOrPrefix)
        {
            var modal = new ModalBuilder().WithTitle($"Update jailbreak prompt for the character")
                                          .WithCustomId($"upd~{webhookIdOrPrefix}")
                                          .AddTextInput("New jailbreak prompt", "new-prompt", TextInputStyle.Paragraph)
                                          .Build();
            await RespondWithModalAsync(modal);
        }

        [SlashCommand("avatar", "Change character avatar")]
        public async Task SetAvatar(string webhookIdOrPrefix, string avatarUrl)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            var channel = (SocketTextChannel)Context.Channel;
            var channelWebhook = await channel.GetWebhookAsync(characterWebhook.Id);

            var image = await TryDownloadImgAsync(avatarUrl, _integration.HttpClient);
            if (image is null && characterWebhook.IntegrationType is IntegrationType.CharacterAI)
            {
                image = new MemoryStream(File.ReadAllBytes($"{EXE_DIR}{SC}storage{SC}default_avatar.png"));
            }

            await channelWebhook.ModifyAsync(cw => cw.Image = new Image(image));

            characterWebhook.Character.AvatarUrl = avatarUrl;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("name", "Change character name")]
        public async Task SetCharacterName(string webhookIdOrPrefix, string name)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            name = name.ToLower().Contains("discord") ? name.Replace('o', 'о').Replace('c', 'с') : name;

            var channel = (SocketTextChannel)Context.Channel;
            var channelWebhook = await channel.GetWebhookAsync(characterWebhook.Id);

            await channelWebhook.ModifyAsync(cw => cw.Name = name);

            characterWebhook.Character.Name = name;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("set-api", "Change API backend")]
        public async Task SetApiBackend(string webhookIdOrPrefix, ApiTypeForChub apiType, OpenAiModel? openAiModel = null, string? personalApiToken = null, string? personalApiEndpoint = null)
        {
            await UpdateApiAsync(webhookIdOrPrefix, apiType, openAiModel, personalApiToken, personalApiEndpoint);
        }

        [SlashCommand("set-delay", "Change response delay")]
        public async Task SetDelay(string webhookIdOrPrefix, int seconds)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.ResponseDelay = seconds;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("toggle-quotes", "Enable/disable quotes")]
        public async Task ToggleQuotes(string webhookIdOrPrefix, bool enable)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.ReferencesEnabled = enable;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("toggle-swipes", "Enable/disable swipe buttons")]
        public async Task ToggleSwipes(string webhookIdOrPrefix, bool enable)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.SwipesEnabled = enable;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("toggle-crutch", "Enable/disable proceed genetaion button")]
        public async Task ToggleCrutch(string webhookIdOrPrefix, bool enable)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.OpenAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Not available for CharacterAI integrations!".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.CrutchEnabled = enable;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("set-cai-history-id", "Change c.ai history ID")]
        public async Task SetCaiHistory(string webhookIdOrPrefix, string newHistoryId)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

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

        [SlashCommand("set-random-reply-chance", "Change random replies chance")]
        public async Task SetRandomReplyChance(string webhookIdOrPrefix, float chance)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.ReplyChance = chance;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("set-max-tokens", "Change amount of tokens for ChatGPT responses")]
        public async Task SetMaxTokens(string webhookIdOrPrefix, int tokens)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

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

            if (tokens > 1000 || tokens < 0)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Availabe values: `0..1000`".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.OpenAiMaxTokens = tokens;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("set-temperature", "Change responses temperature")]
        public async Task SetTemperature(string webhookIdOrPrefix, float temperature)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

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

            if (temperature > 2 || temperature < 0)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Availabe values: `0..2`".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.OpenAiTemperature = temperature;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("set-presence-penalty", "Change responses presence penalty")]
        public async Task SetPresPenalty(string webhookIdOrPrefix, float presencePenalty)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

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

            if (presencePenalty > 2.0 || presencePenalty < -2.0)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Availabe values: `-2..2`".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.OpenAiPresencePenalty = presencePenalty;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("set-frequency-penalty", "Change responses frequency penalty")]
        public async Task SetFreqPenalty(string webhookIdOrPrefix, float frequencyPenalty)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

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

            if (frequencyPenalty > 2.0 || frequencyPenalty < -2.0)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Availabe values: `-2..2`".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.OpenAiFreqPenalty = frequencyPenalty;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }


          ////////////////////
         //// Long stuff ////
        ////////////////////

        private async Task UpdateApiAsync(string webhookIdOrPrefix, ApiTypeForChub apiType, OpenAiModel? openAiModel, string? personalApiToken, string? personalApiEndpont)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

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

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

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

            int refCount = 0;
            if (newFormat.Contains("{{ref_msg_begin}}")) refCount++;
            if (newFormat.Contains("{{ref_msg_text}}")) refCount++;
            if (newFormat.Contains("{{ref_msg_end}}")) refCount++;

            if (refCount != 0 && refCount != 3)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong `ref_msg` placeholder format!".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.MessagesFormat = newFormat;
            await _db.SaveChangesAsync();

            string text = newFormat.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer");

            if (refCount == 3)
            {
                text = text.Replace("{{ref_msg_text}}", "Hola")
                           .Replace("{{ref_msg_begin}}", "")
                           .Replace("{{ref_msg_end}}", "")
                           .Replace("{{ref_msg_user}}", "Dude")
                           .Replace("\\n", "\n");
            }

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **{characterWebhook.Character.Name}**")
                                          .AddField("New format:", $"`{newFormat}`")
                                          .AddField("[Example]", $"User message: *`Hello!`*\n" +
                                                                 $"User nickname: `Average AI Enjoyer`\n" +
                                                                 $"Referenced message: *`Hola`* from user *`Dude`*\n" +
                                                                 $"Result (what character will see): *`{text}`*")
                                          .WithColor(Color.Green)
                                          .Build();

            await FollowupAsync(embed: embed);
        }

    }
}
