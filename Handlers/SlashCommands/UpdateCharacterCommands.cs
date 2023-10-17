using CharacterEngineDiscord.Services;
using Discord;
using Discord.Interactions;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;

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
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            characterWebhook.CallPrefix = newCallPrefix;
            await TryToSaveDbChangesAsync(_db);

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
                                          .AddTextInput("New jailbreak prompt", "new-prompt", TextInputStyle.Paragraph, "(Will not work for CharacterAI)")
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
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            var channel = (SocketTextChannel)Context.Channel;
            var channelWebhook = await channel.GetWebhookAsync(characterWebhook.Id);

            var image = await TryToDownloadImageAsync(avatarUrl, _integration.ImagesHttpClient);
            image ??= new MemoryStream(File.ReadAllBytes($"{EXE_DIR}{SC}storage{SC}default_avatar.png"));

            await channelWebhook.ModifyAsync(cw => cw.Image = new Image(image));

            characterWebhook.Character.AvatarUrl = avatarUrl;
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: SuccessEmbed($"{characterWebhook.Character.Name} avatar updated", imageUrl: avatarUrl));
        }

        [SlashCommand("name", "Change character name")]
        public async Task SetCharacterName(string webhookIdOrPrefix, string name)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            name = name.ToLower().Contains("discord") ? name.Replace('o', 'о').Replace('c', 'с') : name;

            var channel = (SocketTextChannel)Context.Channel;
            var channelWebhook = await channel.GetWebhookAsync(characterWebhook.Id);

            await channelWebhook.ModifyAsync(cw => cw.Name = name);

            string before = characterWebhook.Character.Name;
            characterWebhook.Character.Name = name;
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: SuccessEmbed($"Character name was changed from {before} to {name}"));
        }

        [SlashCommand("set-delay", "Change response delay")]
        public async Task SetDelay(string webhookIdOrPrefix, int seconds)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            string before = characterWebhook.ResponseDelay.ToString();
            characterWebhook.ResponseDelay = seconds;
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: SuccessEmbed($"Response delay was changed from {before}s to {seconds}s"));
        }

        [SlashCommand("toggle-quotes", "Enable/disable quotes")]
        public async Task ToggleQuotes(string webhookIdOrPrefix, bool enable)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            characterWebhook.ReferencesEnabled = enable;
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: SuccessEmbed($"Quotes {(enable ? "enabled" : "disabled")}"));
        }

        [SlashCommand("toggle-swipes", "Enable/disable swipe buttons")]
        public async Task ToggleSwipes(string webhookIdOrPrefix, bool enable)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            characterWebhook.SwipesEnabled = enable;
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: SuccessEmbed($"Swipes {(enable ? "enabled" : "disabled")}"));
        }

        [SlashCommand("toggle-crutch", "Enable/disable proceed genetaion button")]
        public async Task ToggleCrutch(string webhookIdOrPrefix, bool enable)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Not available for CharacterAI integrations".ToInlineEmbed(Color.Red));
                return;
            }

            characterWebhook.CrutchEnabled = enable;
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: SuccessEmbed($"Crutch {(enable ? "enabled" : "disabled")}"));
        }

        [SlashCommand("set-random-reply-chance", "Change random replies chance")]
        public async Task SetRandomReplyChance(string webhookIdOrPrefix, float chance)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            string before = characterWebhook.ReplyChance.ToString();
            characterWebhook.ReplyChance = chance;
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: SuccessEmbed($"Random reply chance for {characterWebhook.Character.Name} was changed from {before} to {chance}"));
        }

        [SlashCommand("set-cai-history-id", "Change c.ai history ID")]
        public async Task SetCaiHistory(string webhookIdOrPrefix, string newHistoryId)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.CharacterAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't set history ID for non-CharacterAI integration".ToInlineEmbed(Color.Red));
                return;
            }

            string message = $"{OK_SIGN_DISCORD} **History ID** for this channel was changed from `{characterWebhook.ActiveHistoryID}` to `{newHistoryId}`";
            if (newHistoryId.Length != 43)
                message += $".\nEntered history ID has length that is different from expected ({newHistoryId.Length}/43). Make sure it's correct.";

            characterWebhook.ActiveHistoryID = newHistoryId;
            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: message.ToInlineEmbed(Color.Green));
        }

        [SlashCommand("open-ai-settings", "Change OpenAI integration settings")]
        public async Task SetOpenAiSettings(string webhookIdOrPrefix, int? maxTokens = null, float? temperature = null, float? frequencyPenalty = null, float? presencePenalty = null, OpenAiModel? openAiModel = null, string? personalApiToken = null, string? personalApiEndpoint = null)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.OpenAI && characterWebhook.IntegrationType is not IntegrationType.Empty)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Not OpenAI intergration or Custom character".ToInlineEmbed(Color.Red));
                return;
            }

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} Settings updated")
                                          .WithColor(Color.Green)
                                          .WithDescription("**Changes:**\n");

            // MaxTokens
            if (maxTokens is not null && (maxTokens < 0 || maxTokens > 1000))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} [max-tokens] Availabe values: `0..1000`".ToInlineEmbed(Color.Red));
                return;
            }
            else if (maxTokens is not null)
            {
                embed.Description += $"- Max tokens value was changed from {characterWebhook.GenerationMaxTokens ?? 200} to {maxTokens}\n";
                characterWebhook.GenerationMaxTokens = maxTokens;
            }

            // Temperature
            if (temperature is not null && (temperature < 0 || temperature > 2))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} [temperature] Availabe values: `0.0 ... 2.0`".ToInlineEmbed(Color.Red));
                return;
            }
            else if (temperature is not null)
            {
                embed.Description += $"- Temperature value was changed from {characterWebhook.GenerationTemperature ?? 1.05} to {temperature}\n";
                characterWebhook.GenerationTemperature = temperature;
            }

            // FreqPenalty
            if (frequencyPenalty is not null && (frequencyPenalty < -2.0 || frequencyPenalty > 2.0))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Availabe values: `-2.0 ... 2.0`".ToInlineEmbed(Color.Red));
                return;
            }
            else if (frequencyPenalty is not null)
            {
                embed.Description += $"- Frequency penalty value was changed from {characterWebhook.GenerationFreqPenaltyOrRepetitionSlope ?? 0.9} to {frequencyPenalty}\n";
                characterWebhook.GenerationFreqPenaltyOrRepetitionSlope = frequencyPenalty;
            }

            // PresPenalty
            if (presencePenalty is not null && (presencePenalty < -2.0 || presencePenalty > 2.0))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} [presence-penalty] Availabe values: `-2.0 ... 2.0`".ToInlineEmbed(Color.Red));
                return;
            }
            else if (presencePenalty is not null)
            {
                embed.Description += $"- Presence penalty value was changed from {characterWebhook.GenerationPresenceOrRepetitionPenalty ?? 0.9} to {presencePenalty}\n";
                characterWebhook.GenerationPresenceOrRepetitionPenalty = presencePenalty;
            }

            // Model
            if (openAiModel is not null)
            {
                var model = openAiModel is OpenAiModel.GPT_3_5_turbo ? "gpt-3.5-turbo" : openAiModel is OpenAiModel.GPT_4 ? "gpt-4" : null;
                embed.Description += $"- Model was changed from {characterWebhook.PersonalApiModel ?? characterWebhook.Channel.Guild.GuildOpenAiModel ?? "`not set`"} to {model}\n";
                characterWebhook.PersonalApiModel = model;
            }

            // Token
            if (personalApiToken is not null)
            {
                embed.Description += $"- Api token was changed from {characterWebhook.PersonalApiToken ?? characterWebhook.Channel.Guild.GuildOpenAiApiToken ?? "`not set`"} to {personalApiToken}\n";
                characterWebhook.PersonalApiToken = personalApiToken;
            }

            // Endpoint
            if (personalApiEndpoint is not null)
            {
                embed.Description += $"- Api endpoint was changed from {characterWebhook.PersonalApiEndpoint ?? characterWebhook.Channel.Guild.GuildOpenAiApiEndpoint?? "`not set`"} to {personalApiEndpoint}\n";
                characterWebhook.PersonalApiEndpoint = personalApiEndpoint;
            }

            await TryToSaveDbChangesAsync(_db);

            await FollowupAsync(embed: embed.Build());
        }

          ////////////////////
         //// Long stuff ////
        ////////////////////

        private async Task UpdateMessagesFormatAsync(string webhookIdOrPrefix, string newFormat)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
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

            characterWebhook.PersonalMessagesFormat = newFormat;
            await TryToSaveDbChangesAsync(_db);

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
