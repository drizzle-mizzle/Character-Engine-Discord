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

        public ShowCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
        }

        [SlashCommand("characters", "Show all characters in this channel")]
        public async Task ShowCharacters(int page = 1)
        {
            try { await ShowCharactersAsync(page); }
            catch (Exception e)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Something went wrong!".ToInlineEmbed(Color.Red));
                LogException(new[] { e });
            }
        }

        [SlashCommand("cai-history-id", "Show c.ai history ID")]
        public async Task CaiHistory(string webhookId)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.CharacterAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't show history id for non-CharacterAI integration!".ToInlineEmbed(Color.Red));
                return;
            }

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Current history ID: `{characterWebhook.CaiActiveHistoryId}`".ToInlineEmbed(Color.Green));
        }

        [SlashCommand("history", "Show last 15 messages with a character. Works only with OpenAI.")]
        public async Task ShowHistory(string webhookId)
        {
            try { await ShowHistoryAsync(webhookId); }
                        catch (Exception e)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Something went wrong!".ToInlineEmbed(Color.Red));
                LogException(new[] { e });
            }
        }

        [SlashCommand("last-request-cost", "~")]
        public async Task ShowLastRequestCost(string webhookId)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Orange));
                return;
            }

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} {characterWebhook.LastRequestTokensUsage.ToString() ?? "?"} tokens".ToInlineEmbed(Color.Green));
        }

        [SlashCommand("messages-format", "Check character messages format")]
        public async Task ShowMessagesFormat(string webhookId)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
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

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
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

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId.Trim()));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Orange));
                return;
            }

            if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't show history for CharacterAI integration!".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.OpenAiHistoryMessages.Count == 0)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} No messages found".ToInlineEmbed(Color.Orange));
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

        private async Task ShowCharactersAsync(int page)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
            if (channel is null || channel.CharacterWebhooks.Count == 0)
            {
                await FollowupAsync(embed: $"{OK_SIGN_DISCORD} No characters were found in this channel".ToInlineEmbed(Color.Orange));
                return;
            }

            var embed = new EmbedBuilder().WithColor(Color.Purple);
            int start = (page - 1) * 5;
            int end = start + 4;

            if ((start + end + 1) > channel.CharacterWebhooks.Count)
                end = channel.CharacterWebhooks.Count - start - 1;

            int count = 0;
            foreach (var cw in channel.CharacterWebhooks)
            {
                string integrationType = cw.IntegrationType is IntegrationType.CharacterAI ? $"**[character.ai](https://beta.character.ai/chat?char={cw.Character.Id})**" :
                                         cw.IntegrationType is IntegrationType.OpenAI ? $"`{cw.OpenAiModel}` **[(chub.ai)](https://www.chub.ai/characters/{cw.Character.Id})**" : "empty";
                string val = $"Call prefix: *`{cw.CallPrefix}`*\n" +
                             $"Integration Type: {integrationType}\n" +
                             $"Webhook ID: *`{cw.Id}`*";

                embed.AddField($"{++count}. {cw.Character.Name}", val);
            }

            double pages = Math.Ceiling(_client.Guilds.Count / 5d);
            embed.WithTitle($"Characters found in this channel: {channel.CharacterWebhooks.Count}");
            embed.WithFooter($"Page {page}/{pages}");

            await FollowupAsync(embed: embed.Build());
        }

    }
}
