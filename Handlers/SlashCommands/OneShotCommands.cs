using Discord.Interactions;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using Discord;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    /// <summary>
    /// Commands that do not change any data
    /// </summary>
    public class OneShotCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly StorageContext _db;
        public OneShotCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("show-history", "Show last 15 messages with a character. Works only with OpenAI.")]
        public async Task ShowHistoryAsync(string webhookId)
        {
            try
            {
                var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
                if (characterWebhook == null)
                {
                    await RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Orange));
                    return;
                }

                if (characterWebhook.OpenAiHistoryMessages.Count == 0)
                {
                    await RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} No messages found", Color.Orange));

                    return;
                }

                int count = characterWebhook.OpenAiHistoryMessages.Count > 15 ? 15 : characterWebhook.OpenAiHistoryMessages.Count;
                var embed = new EmbedBuilder().WithColor(Color.Green)
                                              .WithTitle($"{OK_SIGN_DISCORD} Last {count} messages from the dialog with {characterWebhook.Character.Name}");

                string desc = "";
                for (int i = characterWebhook.OpenAiHistoryMessages.Count - 1; i >= 0; i--)
                {
                    var message = characterWebhook.OpenAiHistoryMessages[i];
                    int l = message.Content.Length > 50 ? 50 : message.Content.Length;
                    desc = $"{count--}. **{(message.Role == "user" ? "User" : characterWebhook.Character.Name)}**: *{message.Content[0..l]}{(l == 50 ? "..." : "")}*\n" + desc;
                }

                await RespondAsync(embed: embed.WithDescription(desc).Build());
            }
            catch (Exception e) { LogException(new[] { e }); }

        }

        [SlashCommand("show-last-request-cost", "~")]
        public async Task ShowLastRequestCost(string webhookId)
        {
            var characterWebhook = await _db.CharacterWebhooks.FindAsync(ulong.Parse(webhookId));
            if (characterWebhook == null)
            {
                await RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Orange));
                return;
            }

            await RespondAsync(embed: InlineEmbed($"{OK_SIGN_DISCORD} {characterWebhook.LastRequestTokensUsage ?? 0} tokens", Color.Green));
        }

        [SlashCommand("help", "Help")]
        public async Task Help()
        {

        }

        [SlashCommand("ping", "Ping")]
        public async Task Ping()
        {
            await RespondAsync("PONG");
        }
    }
}
