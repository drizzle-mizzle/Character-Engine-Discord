using Discord.Interactions;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.WebSocket;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireContext(ContextType.Guild)]
    [Group("show", "Show-commands")]
    public class ShowCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly DiscordSocketClient _client;
        private readonly StorageContext _db;

        public ShowCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("characters", "Show all characters in this channel")]
        public async Task ShowCharacters()
        {
            try { await ShowCharactersAsync(); }
            catch (Exception e) { LogException(new[] { e }); }
        }

        [SlashCommand("cai-history-id", "Show c.ai history ID")]
        public async Task CaiHistory(string webhookId)
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
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Can't show history id for non-CharacterAI integration!", Color.Red));
                return;
            }

            await FollowupAsync(embed: InlineEmbed($"{OK_SIGN_DISCORD} Current history ID: `{characterWebhook.CaiActiveHistoryId}`", Color.Green));
        }

        [SlashCommand("history", "Show last 15 messages with a character. Works only with OpenAI.")]
        public async Task ShowHistory(string webhookId)
        {
            try { await ShowHistoryAsync(webhookId); }
            catch (Exception e) { LogException(new[] { e }); }
        }

        [SlashCommand("last-request-cost", "~")]
        public async Task ShowLastRequestCost(string webhookId)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Orange));
                return;
            }

            await FollowupAsync(embed: InlineEmbed($"{OK_SIGN_DISCORD} {characterWebhook.LastRequestTokensUsage.ToString() ?? "?"} tokens", Color.Green));
        }

        [SlashCommand("messages-format", "Check character messages format")]
        public async Task ShowMessagesFormat(string webhookId)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));

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

        [SlashCommand("jailbreak-prompt", "Check character jailbreak prompt")]
        public async Task ShowJailbreakPrompt(string webhookId)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));

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

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Orange));
                return;
            }

            if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Can't show history for CharacterAI integration!", Color.Red));
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

        private async Task ShowCharactersAsync()
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
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
                                      characterWebhook.IntegrationType is IntegrationType.OpenAI ? characterWebhook.OpenAiModel :
                                      "empty";

                result += $"{count++}. **{characterWebhook.Character.Name}** | *`{characterWebhook.CallPrefix}`* | `{characterWebhook.Id}` | `{integrationType}` \n";
            }
            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} {count} character(s) in this channel:")
                                          .WithDescription(result)
                                          .WithColor(Color.Green)
                                          .Build();

            await FollowupAsync(embed: embed);
        }

    }
}
