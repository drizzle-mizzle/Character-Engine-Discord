using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Discord;
using CharacterEngineDiscord.Models.Common;
using System.Security.Permissions;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireContext(ContextType.Guild)]
    [Group("show", "Show-commands")]
    public class ShowCommands : InteractionModuleBase<InteractionContext>
    {
        public ShowCommands() //IServiceProvider services)
        {
        }

        [SlashCommand("all-characters", "Show all characters in this channel")]
        public async Task ShowCharacters(int page = 1)
        {
            await ShowCharactersAsync(page);
        }

        [SlashCommand("character-info", "Show info about character")]
        public async Task ShowInfo(string webhookIdOrPrefix)
        {
            await ShowInfoAsync(webhookIdOrPrefix);
        }

        [SlashCommand("cai-history-id", "Show c.ai history ID")]
        public async Task ShowCaiHistoryId(string webhookIdOrPrefix)
        {
            await ShowCaiHistoryIdAsync(webhookIdOrPrefix);
        }

        [SlashCommand("dialog-history", "Show last 15 messages with a character")]
        public async Task ShowDialogHistory(string webhookIdOrPrefix)
        {
            await ShowHistoryAsync(webhookIdOrPrefix);
        }

        [SlashCommand("last-request-cost", "~")]
        public async Task ShowLastRequestCost(string webhookIdOrPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Character with the given call prefix or webhook ID was not found in the current channel".ToInlineEmbed(Color.Orange));
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

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Character with the given call prefix or webhook ID was not found in the current channel".ToInlineEmbed(Color.Orange));
                return;
            }

            var character = characterWebhook.Character;
            string statAndLink = characterWebhook.IntegrationType is IntegrationType.CharacterAI ?
                                 $"Original link: [Chat with {character.Name}](https://beta.character.ai/chat?char={character.Id})\nInteractions: {character.Interactions}"
                               : characterWebhook.FromChub ?
                                 $"Original link: [{character.Name} on chub.ai](https://www.chub.ai/characters/{character.Id})\nStars: {character.Stars}"
                               : "Custom character";

            string api = characterWebhook.IntegrationType is IntegrationType.OpenAI ?
                         $"OpenAI ({characterWebhook.PersonalApiModel})"
                       : characterWebhook.IntegrationType is IntegrationType.KoboldAI ?
                         $"KoboldAI ({characterWebhook.PersonalApiModel})"
                       : characterWebhook.IntegrationType is IntegrationType.HordeKoboldAI ?
                         $"Horde KoboldAI ({characterWebhook.PersonalApiModel})"
                       : characterWebhook.IntegrationType.ToString();

            string? info = character.Title;
            if (string.IsNullOrWhiteSpace(info)) info = "No title";
            info = $"Call prefix: *`{characterWebhook.CallPrefix}`*\n" +
                   $"Webhook ID: *`{characterWebhook.Id}`*\n" +
                   $"API: *`{api}`*\n" +
                   $"Messages sent: *`{characterWebhook.MessagesSent}`*\n" +
                   $"Quotes enabled: *`{characterWebhook.ReferencesEnabled}`*\n" +
                   $"Swipes enabled: *`{characterWebhook.SwipesEnabled}`*\n" +
                   $"Proceed button enabled: *`{characterWebhook.CrutchEnabled}`*\n" +
                   $"ResponseDelay: *`{characterWebhook.ResponseDelay}s`*\n" +
                   $"Reply chance: *`{characterWebhook.ReplyChance}%`*\n" +
                   $"Hunted users: *`{characterWebhook.HuntedUsers.Count}`*\n\n" +
                   $"**Tagline**\n*\"{info.Replace("\n\n", "\n")}\"*";

            if (info.Length > 4096) info = info[0..4090] + "[...]";
            
            string? characterDesc = character.Description;
            if (string.IsNullOrWhiteSpace(characterDesc)) characterDesc = "No description";

            characterDesc = (characterDesc.Length > 800 ? characterDesc[0..800] + "[...]" : characterDesc).Replace("\n\n", "\n");
            characterDesc = $"\n\n{characterDesc}\n\n" +
                            $"*{statAndLink}\nCan generate images: {(character.ImageGenEnabled is true ? "Yes" : "No")}*";

            var emb = new EmbedBuilder()
                .WithColor(Color.Gold)
                .WithTitle($"{OK_SIGN_DISCORD} **{character.Name}**")
                .WithDescription(info)
                .AddField("Description", characterDesc)
                .WithImageUrl(characterWebhook.Character.AvatarUrl)
                .WithFooter($"Created by {character.AuthorName}");

            await FollowupAsync(embed: emb.Build());
        }

        private async Task ShowCaiHistoryIdAsync(string webhookIdOrPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.CharacterAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't show history id for non-CharacterAI integration!".ToInlineEmbed(Color.Red));
                return;
            }

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Current history ID: `{characterWebhook.ActiveHistoryID}`".ToInlineEmbed(Color.Green));
        }

        private async Task ShowHistoryAsync(string webhookIdOrPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Character with the given call prefix or webhook ID was not found in the current channel".ToInlineEmbed(Color.Orange));
                return;
            }

            if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't show history for CharacterAI integration!".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.StoredHistoryMessages.Count == 0)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} No messages found".ToInlineEmbed(Color.Orange));
                return;
            }

            int amount = Math.Min(characterWebhook.StoredHistoryMessages.Count, 15);
            var embed = new EmbedBuilder().WithColor(Color.Green).WithTitle($"{OK_SIGN_DISCORD} Last {amount} messages from the dialog with {characterWebhook.Character.Name}");

            var chunks = new List<string>();
            for (int i = characterWebhook.StoredHistoryMessages.Count - 1; i >= 0; i--)
            {
                var message = characterWebhook.StoredHistoryMessages[i];
                int l = Math.Min(message.Content.Length, 250);
                chunks.Add($"{amount--}. **{(message.Role == "user" ? "User" : characterWebhook.Character.Name)}**: *{message.Content[0..l].Replace("\n", "  ").Replace("*", " ")}{(l == 250 ? "..." : "")}*\n");
                if (amount == 0) break;
            }
            chunks.Reverse();
            
            var result = new List<string>() { "" };
            int resultIndex = 0;
            foreach (var chunk in chunks)
            {
                // if string becomes too big, start new
                if ((result.ElementAt(resultIndex).Length + chunk.Length) > 1024)
                {
                    resultIndex++;
                    result.Add(chunk);
                    continue;
                }
                // else, append to it
                result[resultIndex] += chunk;
            }

            for (int i = 0; i < Math.Min(result.Count, 5); i++)
            {
                string newLine = result[i].Length > 1024 ? result[i][0..1018] + "[...]" : result[i];
                embed.AddField("\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~", newLine);
            }
                
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
                format = channel.Guild.GuildMessagesFormat ?? ConfigFile.DefaultMessagesFormat.Value!;
            }
            else
            {
                var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context);

                if (characterWebhook is null)
                {
                    await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE);
                    return;
                }

                title = $"{characterWebhook.Character.Name}'s messages format";
                format = characterWebhook.PersonalMessagesFormat ?? characterWebhook.Channel.Guild.GuildMessagesFormat ?? ConfigFile.DefaultMessagesFormat.Value!;
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
                prompt = channel.Guild.GuildJailbreakPrompt ?? ConfigFile.DefaultJailbreakPrompt.Value!;
            }
            else
            {
                var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context);

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

                title = $"{characterWebhook.Character.Name}'s jailbreak prompt";
                prompt = (characterWebhook.PersonalJailbreakPrompt ?? characterWebhook.Channel.Guild.GuildJailbreakPrompt ?? ConfigFile.DefaultJailbreakPrompt.Value!).Replace("{{char}}", $"{characterWebhook.Character.Name}");
            }

            var embed = new EmbedBuilder().WithTitle($"**{title}**")
                                          .WithColor(Color.Gold);

            var promptChunked = prompt.Chunk(1016);

            foreach (var chunk in promptChunked)
                embed.AddField("\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~\\~", $"```{new string(chunk)}```");

            await FollowupAsync(embed: embed.Build());
        }

        private async Task ShowCharactersAsync(int page)
        {
            await DeferAsync();

            var channelId = Context.Channel is IThreadChannel tc ? tc.CategoryId ?? 0 : Context.Channel.Id;
            var channel = await FindOrStartTrackingChannelAsync(channelId, Context.Guild.Id);
            if (channel is null || channel.CharacterWebhooks.Count == 0)
            {
                await FollowupAsync(embed: $"{OK_SIGN_DISCORD} No characters were found in this channel".ToInlineEmbed(Color.Orange));
                return;
            }

            var embed = new EmbedBuilder().WithColor(Color.Purple);
            int start = (page - 1) * 5;
            int end = (channel.CharacterWebhooks.Count - start) > 5 ? (start + 4) : start + (channel.CharacterWebhooks.Count - start - 1);

            var characterWebhooks = Enumerable.Reverse(channel.CharacterWebhooks);
            for (int i = start; i <= end; i++)
            {
                var cw = characterWebhooks.ElementAt(i);
                string integrationType = cw.IntegrationType is IntegrationType.CharacterAI ? $"**[character.ai](https://beta.character.ai/chat?char={cw.Character.Id})**" :
                                         cw.IntegrationType is IntegrationType.OpenAI ? $"`{cw.PersonalApiModel}` **[(chub.ai)](https://www.chub.ai/characters/{cw.Character.Id})**" : "empty";
                string val = $"Call prefix: *`{cw.CallPrefix}`*\n" +
                             $"Integration Type: {integrationType}\n" +
                             $"Webhook ID: *`{cw.Id}`*\n" +
                             $"Messages sent: *`{cw.MessagesSent}`*";

                embed.AddField($"{++start}. {cw.Character.Name}", val);
            }

            double pages = Math.Ceiling(channel.CharacterWebhooks.Count / 5d);
            embed.WithTitle($"Characters found in this channel: {channel.CharacterWebhooks.Count}");
            embed.WithFooter($"Page {page}/{pages}");

            await FollowupAsync(embed: embed.Build());
        }

    }
}
