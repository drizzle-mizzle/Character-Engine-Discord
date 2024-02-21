using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.DatabaseContext;
using Discord;
using CharacterEngineDiscord.Models.Common;
using Discord.WebSocket;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireContext(ContextType.Guild)]
    [Group("show", "Show-commands")]
    public class ShowCommands() : InteractionModuleBase<InteractionContext>
    {
        //private readonly DiscordSocketClient _client = (DiscordSocketClient)client;


        [SlashCommand("all-characters", "Show all characters in this channel")]
        public async Task ShowCharacters(int page = 1, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            var channelId = Context.Channel is IThreadChannel tc ? tc.CategoryId ?? 0 : Context.Channel.Id;

            await using var db = new DatabaseContext();
            var channel = await FindOrStartTrackingChannelAsync(channelId, Context.Guild.Id, db);
            if (channel.CharacterWebhooks.Count == 0)
            {
                await FollowupAsync(embed: $"{OK_SIGN_DISCORD} No characters were found in this channel".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            var embed = new EmbedBuilder().WithColor(Color.Purple);
            int start = (page - 1) * 5;
            int end = (channel.CharacterWebhooks.Count - start) > 5 ? (start + 4) : start + (channel.CharacterWebhooks.Count - start - 1);

            var characterWebhooks = Enumerable.Reverse(channel.CharacterWebhooks).ToList();
            for (int i = start; i <= end; i++)
            {
                var cw = characterWebhooks.ElementAt(i);
                string integrationType = cw.IntegrationType is IntegrationType.CharacterAI ?
                                            $"**[CharacterAI](https://beta.character.ai/chat?char={cw.Character.Id})**"
                                       : cw.IntegrationType is IntegrationType.OpenAI ?
                                            $"`OpenAI {cw.PersonalApiModel}` **{(cw.FromChub ? $"[(chub.ai)](https://www.chub.ai/characters/{cw.Character.Id})" : "(custom character)")}**"
                                       : cw.IntegrationType is IntegrationType.KoboldAI ?
                                            $"`KoboldAI` **{(cw.FromChub ? $"[(chub.ai)](https://www.chub.ai/characters/{cw.Character.Id})" : "(custom character)")}**"
                                       : cw.IntegrationType is IntegrationType.HordeKoboldAI ?
                                            $"`Horde KoboldAI` **{(cw.FromChub ? $"[(chub.ai)](https://www.chub.ai/characters/{cw.Character.Id})" : "(custom character)")}**"
                                       : "not set";

                string val = $"Call prefix: *`{cw.CallPrefix}`*\n" +
                             $"Integration Type: {integrationType}\n" +
                             $"Webhook ID: *`{cw.Id}`*\n" +
                             $"Messages sent: *`{cw.MessagesSent}`*";

                embed.AddField($"{++start}. {cw.Character.Name}", val);
            }

            double pages = Math.Ceiling(channel.CharacterWebhooks.Count / 5d);
            embed.WithTitle($"Characters found in this channel: {channel.CharacterWebhooks.Count}");
            embed.WithFooter($"Page {page}/{pages}");

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }


        [SlashCommand("character-info", "Show info about character")]
        public async Task ShowInfo(string webhookIdOrPrefix, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new DatabaseContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Character with the given call prefix or webhook ID was not found in the current channel".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            var character = characterWebhook.Character;

            var _tagline = character.Title?.Replace("\n\n", "\n");
            if (string.IsNullOrWhiteSpace(_tagline)) _tagline = "No title";

            string? _characterDesc = character.Description?.Replace("\n\n", "\n");
            if (string.IsNullOrWhiteSpace(_characterDesc)) _characterDesc = "No description";

            string _statAndLink = characterWebhook.IntegrationType is IntegrationType.CharacterAI ?
                          $"Original link: [Chat with {character.Name}](https://beta.character.ai/chat?char={character.Id})\nInteractions: `{character.Interactions}`"                                
                                : characterWebhook.FromChub ?
                          $"Original link: [{character.Name} on chub.ai](https://www.chub.ai/characters/{character.Id})\nStars: `{character.Stars}`"
                                : "Custom character";
            string _api = characterWebhook.IntegrationType is IntegrationType.OpenAI ?
                          $"OpenAI ({characterWebhook.PersonalApiModel})"
                        : characterWebhook.IntegrationType is IntegrationType.KoboldAI ?
                          $"KoboldAI ({characterWebhook.PersonalApiModel})"
                        : characterWebhook.IntegrationType is IntegrationType.HordeKoboldAI ?
                          $"Horde KoboldAI ({characterWebhook.PersonalApiModel})"
                        : characterWebhook.IntegrationType.ToString();

            string details = $"*{_statAndLink}\nCan generate images: {(character.ImageGenEnabled is true ? "Yes" : "No")}*";
            string info = $"Call prefix: *`{characterWebhook.CallPrefix}`*\n" +
                          $"Webhook ID: *`{characterWebhook.Id}`*\n" +
                          $"API: *`{_api}`*\n" +
                          $"Messages sent: *`{characterWebhook.MessagesSent}`*\n" +
                          $"Quotes enabled: *`{characterWebhook.ReferencesEnabled}`*\n" +
                          $"Swipes enabled: *`{characterWebhook.SwipesEnabled}`*\n" +
                          $"Proceed button enabled: *`{characterWebhook.CrutchEnabled}`*\n" +
                          $"ResponseDelay: *`{characterWebhook.ResponseDelay}s`*\n" +
                          $"Reply chance: *`{characterWebhook.ReplyChance}%`*\n" +
                          $"Hunted users: *`{characterWebhook.HuntedUsers.Count}`*";
            string fullDesc = $"*{_tagline}*\n\n**Description**\n{_characterDesc}";

            if (fullDesc.Length > 4096)
                fullDesc = fullDesc[..4090] + "[...]";

            var emb = new EmbedBuilder()
                .WithColor(Color.Gold)
                .WithTitle($"{OK_SIGN_DISCORD} **{character.Name}**")
                .WithDescription($"{fullDesc}\n")
                .AddField("Details", details)
                .AddField("Settings", info)
                .WithImageUrl(characterWebhook.Character.AvatarUrl)
                .WithFooter($"Created by {character.AuthorName}");

            await FollowupAsync(embed: emb.Build(), ephemeral: silent);
        }


        [SlashCommand("cai-history-id", "Show c.ai history ID")]
        public async Task ShowCaiHistoryId(string webhookIdOrPrefix, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new DatabaseContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                return;
            }

            if (characterWebhook.IntegrationType is not IntegrationType.CharacterAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't show history id for non-CharacterAI integration!".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} Current history ID: `{characterWebhook.ActiveHistoryID}`".ToInlineEmbed(Color.Green), ephemeral: silent);
        }


        [SlashCommand("dialog-history", "Show last 15 messages with a character")]
        public async Task ShowDialogHistory(string webhookIdOrPrefix, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new DatabaseContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Character with the given call prefix or webhook ID was not found in the current channel".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            var type = characterWebhook.IntegrationType;
            if (type is not IntegrationType.OpenAI && type is not IntegrationType.KoboldAI && type is not IntegrationType.HordeKoboldAI)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't show history for {type} integration".ToInlineEmbed(Color.Red), ephemeral: silent);
                return;
            }

            if (characterWebhook.StoredHistoryMessages.Count == 0)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} No messages found".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            int amount = Math.Min(characterWebhook.StoredHistoryMessages.Count, 15);
            var embed = new EmbedBuilder().WithColor(Color.Green).WithTitle($"{OK_SIGN_DISCORD} Last {amount} messages from the dialog with {characterWebhook.Character.Name}");

            var chunks = new List<string>();
            for (int i = characterWebhook.StoredHistoryMessages.Count - 1; i >= 0; i--)
            {
                var message = characterWebhook.StoredHistoryMessages[i];
                int l = Math.Min(message.Content.Length, 250);
                chunks.Add($"{amount--}. **{(message.Role == "user" ? "User" : characterWebhook.Character.Name)}**: *{message.Content[..l].Replace("\n", "  ").Replace("*", " ")}{(l == 250 ? "..." : "")}*\n");
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
                embed.AddField(@"\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~", newLine);
            }

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }


        [SlashCommand("last-request-cost", "~")]
        public async Task ShowLastRequestCost(string webhookIdOrPrefix, bool silent = false)
        {
            await DeferAsync(ephemeral: silent);

            await using var db = new DatabaseContext();
            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Character with the given call prefix or webhook ID was not found in the current channel".ToInlineEmbed(Color.Orange), ephemeral: silent);
                return;
            }

            await FollowupAsync(embed: $"{OK_SIGN_DISCORD} {characterWebhook.LastRequestTokensUsage.ToString() ?? "?"} tokens".ToInlineEmbed(Color.Green), ephemeral: silent);
        }


        [SlashCommand("messages-format", "Check default or character messages format")]
        public async Task ShowMessagesFormat(string? webhookIdOrPrefix = null, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            string title;
            string format;

            await using var db = new DatabaseContext();
            if (webhookIdOrPrefix is null)
            {
                var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, db);
                title = "Default messages format";
                format = channel.Guild.GuildMessagesFormat ?? ConfigFile.DefaultMessagesFormat.Value!;
            }
            else
            {
                var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

                if (characterWebhook is null)
                {
                    await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
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

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }


        [SlashCommand("jailbreak-prompt", "Check default or character jailbreak prompt")]
        public async Task ShowJailbreakPrompt(string? webhookIdOrPrefix = null, bool silent = true)
        {
            await DeferAsync(ephemeral: silent);

            string title;
            string prompt;

            await using var db = new DatabaseContext();
            if (webhookIdOrPrefix is null)
            {
                var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, db);

                title = "Default jailbreak prompt";
                prompt = channel.Guild.GuildJailbreakPrompt ?? ConfigFile.DefaultJailbreakPrompt.Value!;
            }
            else
            {
                var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, db);

                if (characterWebhook is null)
                {
                    await FollowupAsync(embed: CHARACTER_NOT_FOUND_MESSAGE, ephemeral: silent);
                    return;
                }

                var type = characterWebhook.IntegrationType;
                if (type is IntegrationType.CharacterAI)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Not available for {type} integrations".ToInlineEmbed(Color.Red), ephemeral: silent);
                    return;
                }

                title = $"{characterWebhook.Character.Name}'s jailbreak prompt";
                prompt = (characterWebhook.PersonalJailbreakPrompt ?? characterWebhook.Channel.Guild.GuildJailbreakPrompt ?? ConfigFile.DefaultJailbreakPrompt.Value!).Replace("{{char}}", $"{characterWebhook.Character.Name}");
            }

            var embed = new EmbedBuilder().WithTitle($"**{title}**")
                                          .WithColor(Color.Gold);

            var promptChunked = prompt.Chunk(1016);

            foreach (var chunk in promptChunked)
                embed.AddField(@"\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~\~", $"```{new string(chunk)}```");

            await FollowupAsync(embed: embed.Build(), ephemeral: silent);
        }
    }
}
