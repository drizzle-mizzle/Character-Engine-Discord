using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Models.Database;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;
using CharacterEngineDiscord.Models.Common;
using Discord.WebSocket;

namespace CharacterEngineDiscord.Services
{
    public static partial class CommandsService
    {
        internal static async Task<CharacterWebhook?> TryToFindCharacterWebhookInChannelAsync(string webhookIdOrPrefix, InteractionContext context, StorageContext? _db = null)
        {
            var channel = await FindOrStartTrackingChannelAsync(context.Channel.Id, context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());

            if (characterWebhook is null)
            {
                bool ok = ulong.TryParse(webhookIdOrPrefix.Trim(), out var cwId);
                characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == (ok ? cwId : 0));
            }

            return characterWebhook;
        }

        internal static async Task<CharacterWebhook?> TryToFindCharacterWebhookInChannelAsync(string webhookIdOrPrefix, ulong channelId, StorageContext _db)
        {
            var channel = await _db.Channels.FindAsync(channelId);
            if (channel is null) return null;

            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == webhookIdOrPrefix.Trim());

            if (characterWebhook is null)
            {
                bool ok = ulong.TryParse(webhookIdOrPrefix.Trim(), out var cwId);
                characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == (ok ? cwId : 0));
            }

            return characterWebhook;
        }

        internal static string GetBestName(this SocketGuildUser user)
            => user.Nickname ?? user.DisplayName ?? user.Username;

        internal static bool IsHoster(this SocketGuildUser? user)
        {
            string? hosterId = ConfigFile.HosterDiscordID.Value;

            try
            {
                return hosterId is not null && user is not null && user.Id == ulong.Parse(hosterId);
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                return false;
            }
        }

        internal static bool IsServerOwner(this SocketGuildUser? user)
            => user is not null && user.Id == user.Guild.OwnerId;

        internal static bool HasManagerRole(this SocketGuildUser? user)
            => user is not null && user.Roles.Any(r => r.Name == ConfigFile.DiscordBotRole.Value);

        internal static async Task SendNoPowerFileAsync(this IInteractionContext context)
        {
            try
            {
                await context.Interaction.DeferAsync();
                var filename = ConfigFile.NoPermissionFile.Value;
                if (filename is null) return;

                var stream = File.OpenRead($"{EXE_DIR}{SC}storage{SC}{filename}");
                await context.Interaction.FollowupWithFileAsync(stream, filename);
            }
            catch (Exception e)
            {
                LogException(new[] { e });
            }
        }

        internal static Embed SpawnCharacterEmbed(CharacterWebhook characterWebhook)
        {
            var character = characterWebhook.Character;
            var (link, stat) = characterWebhook.IntegrationType == IntegrationType.CharacterAI ?
                ($"[Chat with {character.Name}](https://beta.character.ai/chat?char={character.Id})", $"Interactions: {character.Interactions}") :
                ($"[{character.Name} on chub.ai](https://www.chub.ai/characters/{character.Id})", $"Stars: {character.Stars}");

            string? title = character.Title;
            if (string.IsNullOrWhiteSpace(title)) title = "No title";

            title = (title.Length > 1000 ? title[0..1000] + "[...]" : title).Replace("\n\n", "\n");
            title = $"*\"{title}\"*";


            string? desc = character.Description;
            if (string.IsNullOrWhiteSpace(desc)) desc = "No description";

            desc = (desc.Length > 800 ? desc[0..800] + "[...]" : desc).Replace("\n\n", "\n");
            desc = $"\n\n{desc}\n\n*Original link: {link}\n" +
                   $"Can generate images: {(character.ImageGenEnabled is true ? "Yes" : "No")}\n{stat}*";

            var emb = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **Success**").WithColor(Color.Gold);
            emb.WithDescription($"Use *`\"{characterWebhook.CallPrefix}\"`* prefix or replies to call the character.");
            emb.AddField("Configuration", $"Webhook ID: *`{characterWebhook.Id}`*\nUse it or the prefix to modify this integration with *`/update`* commands.");
            emb.AddField("Usage example:", $"*`{characterWebhook.CallPrefix} hey!`*\n" +
                                           $"*`/update call-prefix webhook-id-or-prefix:{characterWebhook.CallPrefix} new-call-prefix:ai`*");
            emb.AddField(characterWebhook.Character.Name ?? "???", title);
            emb.AddField("Description", desc);
            emb.WithFooter($"Created by {character.AuthorName}");
            if (!string.IsNullOrWhiteSpace(characterWebhook.Character.AvatarUrl))
                emb.WithImageUrl(characterWebhook.Character.AvatarUrl);

            return emb.Build();
        }

        /// <summary>
        /// Creates and sends character selection menu
        /// </summary>
        /// <returns>SearchQuery object linked to the created selection menu</returns>
        internal static async Task<SearchQuery?> BuildAndSendSelectionMenuAsync(InteractionContext context, SearchQueryData searchQueryData)
        {
            if (!searchQueryData.IsSuccessful)
            {
                await context.Interaction.ModifyOriginalResponseAsync(msg => msg.Embed = $"{WARN_SIGN_DISCORD} Failed to find a character: `{searchQueryData.ErrorReason}`".ToInlineEmbed(Color.Red));
                return null;
            }

            if (searchQueryData.IsEmpty)
            {
                await context.Interaction.ModifyOriginalResponseAsync(msg => msg.Embed = $"{WARN_SIGN_DISCORD} No characters were found".ToInlineEmbed(Color.Orange));
                return null;
            }

            await FindOrStartTrackingChannelAsync(context.Channel.Id, context.Guild.Id);

            int pages = (int)Math.Ceiling(searchQueryData.Characters.Count / 10.0f);
            var query = new SearchQuery(context.Channel.Id, context.User.Id, searchQueryData, pages);
            var list = BuildCharactersList(query);
            var buttons = BuildSelectButtons(query);
            await context.Interaction.ModifyOriginalResponseAsync(msg => { msg.Embed = list; msg.Components = buttons; });

            return query; // further logic is handled by the ButtonsAndReactionsHandler()
        }

        public static Embed BuildCharactersList(SearchQuery query)
        {
            var list = new EmbedBuilder().WithTitle($"({query.SearchQueryData.Characters.Count}) Characters found by query \"{query.SearchQueryData.OriginalQuery}\":")
                                         .WithFooter($"Page {query.CurrentPage}/{query.Pages}")
                                         .WithColor(Color.Green);
            // Fill with first 10 or less
            int tail = query.SearchQueryData.Characters.Count - (query.CurrentPage - 1) * 10;
            int rows = tail > 10 ? 10 : tail;

            for (int i = 0; i < rows; i++)
            {
                int index = (query.CurrentPage - 1) * 10 + i;
                var character = query.SearchQueryData.Characters[index];
                string fTitle = character.Name!;

                if (i + 1 == query.CurrentRow) fTitle += " - ✅";

                string interactionsOrStars = query.SearchQueryData.IntegrationType is IntegrationType.CharacterAI ?
                    $"Interactions: {character.Interactions}" :
                    $"Stars: {character.Stars}";

                list.AddField($"{index + 1}. {fTitle}", $"{interactionsOrStars} | Author: {character.AuthorName}");
            }

            return list.Build();
        }
        public static MessageComponent BuildSelectButtons(SearchQuery query)
        {
            // List navigation buttons
            var buttons = new ComponentBuilder()
                .WithButton(emote: new Emoji("\u2B06"), customId: $"up", style: ButtonStyle.Secondary)
                .WithButton(emote: new Emoji("\u2B07"), customId: $"down", style: ButtonStyle.Secondary)
                .WithButton(emote: new Emoji("\u2705"), customId: $"select", style: ButtonStyle.Success);
            // Pages navigation buttons
            if (query.Pages > 1) buttons
                .WithButton(emote: new Emoji("\u2B05"), customId: $"left", row: 1)
                .WithButton(emote: new Emoji("\u27A1"), customId: $"right", row: 1);

            return buttons.Build();
        }

        public static async Task TryToReportInLogsChannel(IDiscordClient client, string title, string desc, string? content, Color color, bool error)
        {
            string? channelId = null;

            if (error) channelId = ConfigFile.DiscordErrorLogsChannelID.Value;
            if (channelId.IsEmpty()) channelId = ConfigFile.DiscordLogsChannelID.Value;
            if (channelId.IsEmpty()) return;

            if (!ulong.TryParse(channelId, out var uChannelId)) return;

            var channel = await client.GetChannelAsync(uChannelId);
            if (channel is not ITextChannel textChannel) return;

            await ReportInLogsChannel(textChannel, title, desc, content, color);
        }

        public static async Task ReportInLogsChannel(ITextChannel channel, string title, string desc, string? content, Color color)
        { 
            try
            {
                var embed = new EmbedBuilder().WithTitle(title).WithColor(color);

                if (content is not null)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        if (content.Length > 1010)
                        {
                            embed.AddField("\\~\\~\\~\\~\\~\\~\\~\\~\\~", $"```cs\n{content[0..1009]}...```");
                            content = content[1009..];
                        }
                        else
                        {
                            embed.AddField("\\~\\~\\~\\~\\~\\~\\~\\~\\~", $"```cs\n{content}```");
                            break;
                        }
                    }
                }

                await channel.SendMessageAsync(embed: embed.WithDescription(desc).Build());
            }
            catch (Exception e)
            {
                LogException(new[] { e });
            }
        }

        public enum OpenAiModel
        {
            [ChoiceDisplay("gpt-3.5-turbo")]
            GPT_3_5_turbo,

            [ChoiceDisplay("gpt-4")]
            GPT_4
        }

        public enum ApiTypeForChub
        {
            OpenAI
        }
            }
}
