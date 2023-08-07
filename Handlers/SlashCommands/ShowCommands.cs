using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Discord;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireContext(ContextType.Guild)]
    [Group("show", "Show-commands")]
    public class ShowCommands : InteractionModuleBase<InteractionContext>
    {
        //private readonly IntegrationsService _integration;
        //private readonly DiscordSocketClient _client;

        public ShowCommands(IServiceProvider services)
        {
            //_integration = services.GetRequiredService<IntegrationsService>();
            //_client = services.GetRequiredService<DiscordSocketClient>();
        }

        [SlashCommand("characters", "Show all characters in this channel")]
        public async Task ShowCharacters(int page = 1)
        {
            await ShowCharactersAsync(page);
        }

        [SlashCommand("info", "Show info about character")]
        public async Task ShowInfo(string webhookIdOrPrefix)
        {
            await ShowInfoAsync(webhookIdOrPrefix);
        }

        [SlashCommand("cai-history-id", "Show c.ai history ID")]
        public async Task CaiHistory(string webhookIdOrPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookAsync(webhookIdOrPrefix, Context);

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
        public async Task ShowHistory(string webhookIdOrPrefix)
        {
            await ShowHistoryAsync(webhookIdOrPrefix);
        }

        [SlashCommand("last-request-cost", "~")]
        public async Task ShowLastRequestCost(string webhookIdOrPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookAsync(webhookIdOrPrefix, Context);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Orange));
                return;
            }

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} {characterWebhook.LastRequestTokensUsage.ToString() ?? "?"} tokens".ToInlineEmbed(Color.Green));
        }

        [SlashCommand("messages-format", "Check default or character messages format")]
        public async Task ShowMessagesFormat(string? webhookIdOrPrefix = null)
        {
            await ShowMessagesFormatAsync(webhookIdOrPrefix);
        }

        [SlashCommand("jailbreak-prompt", "Check default or character jailbreak prompt")]
        public async Task ShowJailbreakPrompt(string? webhookIdOrPrefix = null)
        {
            await ShowJailbreakPromptAsync(webhookIdOrPrefix);
        }


        ////////////////////
        //// Long stuff ////
        ////////////////////

        private async Task ShowInfoAsync(string webhookIdOrPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookAsync(webhookIdOrPrefix, Context);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Orange));
                return;
            }

            var character = characterWebhook.Character;
            var (link, stat) = characterWebhook.IntegrationType == IntegrationType.CharacterAI ?
                ($"[Chat with {character.Name}](https://beta.character.ai/chat?char={character.Id})", $"Interactions: {character.Interactions}") :
                ($"[{character.Name} on chub.ai](https://www.chub.ai/characters/{character.Id})", $"Stars: {character.Stars}");

            string? title = character.Title;
            if (string.IsNullOrWhiteSpace(title)) title = "No title";
            title = (title.Length > 800 ? title[0..800] + "[...]" : title).Replace("\n\n", "\n");
            title = $"Swipes enabled: {characterWebhook.SwipesEnabled}\n" +
                    $"ResponseDelay: {characterWebhook.ResponseDelay}s\n" +
                    $"Reply chance: {characterWebhook.ReplyChance}\n" +
                    $"Call prefix: *`{characterWebhook.CallPrefix}`*\n" +
                    $"Webhook ID: *`{characterWebhook.Id}`*\n\n\"{title}\"";
            
            string? desc = character.Description;
            if (string.IsNullOrWhiteSpace(desc)) desc = "No description";

            desc = (desc.Length > 800 ? desc[0..800] + "[...]" : desc).Replace("\n\n", "\n");
            desc = $"\n\n{desc}\n\n*Original link: {link}\n" +
                   $"Can generate images: {(character.ImageGenEnabled is true ? "Yes" : "No")}\n{stat}*";

            var emb = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **{character.Name}**").WithColor(Color.Gold);
            emb.WithDescription(title);
            emb.AddField("Description", desc);
            emb.WithImageUrl(characterWebhook.Character.AvatarUrl);
            emb.WithFooter($"Created by {character.AuthorName}");

            await FollowupAsync(embed: emb.Build());
        }

        private async Task ShowHistoryAsync(string webhookIdOrPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookAsync(webhookIdOrPrefix, Context);

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

            int amount = Math.Min(characterWebhook.OpenAiHistoryMessages.Count, 30);
            var embed = new EmbedBuilder().WithColor(Color.Green).WithTitle($"{OK_SIGN_DISCORD} Last {amount} messages from the dialog with {characterWebhook.Character.Name}");

            var chunks = new List<string>();
            for (int i = characterWebhook.OpenAiHistoryMessages.Count - 1; i >= 0; i--)
            {
                var message = characterWebhook.OpenAiHistoryMessages[i];
                int l = Math.Min(message.Content.Length, 200);
                chunks.Add($"{amount--}. **{(message.Role == "user" ? "User" : characterWebhook.Character.Name)}**: *{message.Content[0..l].Replace("\n", "  ")}{(l == 200 ? "..." : "")}*\n");
            }
            chunks.Reverse();

            var result = new List<string>() { "" };
            int resultIndex = 0;
            foreach (var chunk in chunks)
            {
                if ((result.ElementAt(resultIndex).Length + chunk.Length) > 1024)
                {
                    resultIndex++;
                    result.Add(chunk);
                    continue;
                }
                
                result[resultIndex] += chunk;
            }

            for (int i = 0; i < Math.Min(result.Count, 5); i++)
                embed.AddField("\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~", result[i]);

            await FollowupAsync(embed: embed.Build());
        }

        private async Task ShowMessagesFormatAsync(string? webhookIdOrPrefix)
        {
            await DeferAsync();

            string title;
            string format;

            if (webhookIdOrPrefix is null)
            {
                var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);
                title = "Default messages format";
                format = channel.Guild.GuildMessagesFormat;
            }
            else
            {
                var characterWebhook = await TryToFindCharacterWebhookAsync(webhookIdOrPrefix, Context);

                if (characterWebhook is null)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                    return;
                }

                title = $"{characterWebhook.Character.Name}'s messages format";
                format = characterWebhook.MessagesFormat;
            }

            string text = format.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer");

            if (text.Contains("{{ref_msg_text}}"))
            {
                text = text.Replace("{{ref_msg_text}}", "Hola")
                           .Replace("{{ref_msg_begin}}", "")
                           .Replace("{{ref_msg_end}}", "")
                           .Replace("{{ref_msg_user}}", "Dude")
                           .Replace("\\n", "\n");
            }

            var embed = new EmbedBuilder().WithTitle($"{title}")
                                          .WithColor(Color.Gold)
                                          .AddField("Format:", $"`{format}`")
                                          .AddField("Example", $"Referenced message: *`Hola`* from user *`Dude`*\n" +
                                                               $"User nickname: `Average AI Enjoyer`\n" +
                                                               $"User message: *`Hello!`*\n" +
                                                               $"Result (what character will see):\n*`{text}`*");

            await FollowupAsync(embed: embed.Build());
        }

        private async Task ShowJailbreakPromptAsync(string? webhookIdOrPrefix)
        {
            await DeferAsync();

            string title;
            string prompt;

            if (webhookIdOrPrefix is null)
            {
                var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id);

                title = "Default jailbreak prompt";
                prompt = channel.Guild.GuildJailbreakPrompt ?? "";
            }
            else
            {
                var characterWebhook = await TryToFindCharacterWebhookAsync(webhookIdOrPrefix, Context);

                if (characterWebhook is null)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                    return;
                }

                title = $"{characterWebhook.Character.Name}'s jailbreak prompt";
                prompt = characterWebhook.UniversalJailbreakPrompt ?? "";
            }

            var embed = new EmbedBuilder().WithTitle($"**{title}**")
                                          .WithColor(Color.Gold);

            var promptChunked = prompt.Chunk(1024);

            foreach (var chunk in promptChunked)
                embed.AddField("\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~", new string(chunk));

            await FollowupAsync(embed: embed.Build());
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
            int end = (channel.CharacterWebhooks.Count - start) > 5 ? (start + 4) : start + (channel.CharacterWebhooks.Count - start - 1);

            int count = 0;
            var characterWebhooks = Enumerable.Reverse(channel.CharacterWebhooks);
            for (int i = start; i <= end; i++)
            {
                var cw = characterWebhooks.ElementAt(i);
                string integrationType = cw.IntegrationType is IntegrationType.CharacterAI ? $"**[character.ai](https://beta.character.ai/chat?char={cw.Character.Id})**" :
                                         cw.IntegrationType is IntegrationType.OpenAI ? $"`{cw.OpenAiModel}` **[(chub.ai)](https://www.chub.ai/characters/{cw.Character.Id})**" : "empty";
                string val = $"Call prefix: *`{cw.CallPrefix}`*\n" +
                             $"Integration Type: {integrationType}\n" +
                             $"Webhook ID: *`{cw.Id}`*";

                embed.AddField($"{++count}. {cw.Character.Name}", val);
            }

            double pages = Math.Ceiling(channel.CharacterWebhooks.Count / 5d);
            embed.WithTitle($"Characters found in this channel: {channel.CharacterWebhooks.Count}");
            embed.WithFooter($"Page {page}/{pages}");

            await FollowupAsync(embed: embed.Build());
        }

    }
}
