using Discord;
using Discord.Webhook;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireContext(ContextType.Guild)]
    public class PerCharacterCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly StorageContext _db;

        public PerCharacterCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("show-history", "Show last 15 messages with a character. Works only with OpenAI.")]
        public async Task ShowHistory(string webhookId)
        {
            try { await ShowHistoryAsync(webhookId); }
            catch (Exception e) { LogException(new[] { e }); }
        }

        [SlashCommand("show-last-request-cost", "~")]
        public async Task ShowLastRequestCost(string webhookId)
        {
            await DeferAsync();

            var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
            if (characterWebhook == null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Orange));
                return;
            }

            await FollowupAsync(embed: InlineEmbed($"{OK_SIGN_DISCORD} {characterWebhook.LastRequestTokensUsage.ToString() ?? "?"} tokens", Color.Green));
        }

        [SlashCommand("show-messages-format", "Check character messages format")]
        public async Task ShowMessagesFormat(string webhookId)
        {
            await DeferAsync();

            var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            var embed = new EmbedBuilder().WithTitle($"**{characterWebhook.Character.Name}**")
                                          .WithColor(Color.Gold)
                                          .AddField("Format:", $"`{characterWebhook.MessagesFormat}`")
                                          .AddField("[Example]", $"User message: *`Hello!`*\nResult (what character will see): *`{characterWebhook.MessagesFormat.Replace("{{msg}}", "Hello!")}`*")
                                          .Build();

            await FollowupAsync(embed: embed);
        }

        [SlashCommand("show-jailbreak-prompt", "Check character jailbreak prompt")]
        public async Task ShowJailbreakPrompt(string webhookId)
        {
            await DeferAsync();

            var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            var embed = new EmbedBuilder().WithTitle($"**{characterWebhook.Character.Name}**")
                                          .WithColor(Color.Gold)
                                          .AddField("Prompt:", $"{characterWebhook.UniversalJailbreakPrompt}")
                                          .Build();

            await FollowupAsync(embed: embed);
        }

        ////////////////////////////
        //// Main logic section ////
        ////////////////////////////

        private async Task ShowHistoryAsync(string webhookId)
        {
            await DeferAsync();

            var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
            if (characterWebhook == null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Orange));
                return;
            }

            if (characterWebhook.OpenAiHistoryMessages.Count == 0)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} No messages found", Color.Orange));

                return;
            }

            int count = characterWebhook.OpenAiHistoryMessages.Count > 15 ? 15 : characterWebhook.OpenAiHistoryMessages.Count;
            var embed = new EmbedBuilder().WithColor(Color.Green)
                                          .WithTitle($"{OK_SIGN_DISCORD} Last {count} messages from the dialog with {characterWebhook.Character.Name}");

            string desc = "";
            for (int i = characterWebhook.OpenAiHistoryMessages.Count - 1; i >= 0; i--)
            {
                var message = characterWebhook.OpenAiHistoryMessages[i];
                int l = message.Content.Length > 60 ? 60 : message.Content.Length;
                desc = $"{count--}. **{(message.Role == "user" ? "User" : characterWebhook.Character.Name)}**: *{message.Content[0..l]}{(l == 60 ? "..." : "")}*\n" + desc;
            }

            await FollowupAsync(embed: embed.WithDescription(desc).Build());
        }
    }
}
