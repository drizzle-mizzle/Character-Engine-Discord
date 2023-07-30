using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Models.Database;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;
using CharacterEngineDiscord.Models.Common;
using Discord.WebSocket;
using System.Text.RegularExpressions;

namespace CharacterEngineDiscord.Services
{
    public static partial class CommandsService
    {
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

        internal static string RemoveFirstMentionPrefx(this string text)
            => MentionRegex().Replace(text, "", 1).Trim();

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

            string? title = (character.Title ?? "").Length > 300 ? character.Title!.Replace("\n\n", "\n")[0..300] + "[...]" : character.Title;
            string? desc = (character.Description ?? "").Length > 400 ? character.Description!.Replace("\n\n", "\n")[0..300] + "[...]" : character.Description;
            if (!string.IsNullOrWhiteSpace(desc)) desc = $"\n\n{desc}\n\n";

            string lastField = $"*\"{title}\"*" +
                               $"{desc}" +
                               $"*Original link: {link}\n" +
                               $"Can generate images: {(character.ImageGenEnabled is true ? "Yes" : "No")}\n{stat}*";

            var emb = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **Success**").WithColor(Color.Gold);
            emb.WithDescription($"Use *`\"{characterWebhook.CallPrefix}\"`* prefix or replies to call the character.");
            emb.AddField("Usage example:", $"*`{characterWebhook.CallPrefix}hey!`*");
            emb.AddField("Configuration", $"Webhook ID: *`{characterWebhook.Id}`*\nUse it or the prefix to modify this integration with *`/update-character`* commands.");
            emb.AddField(characterWebhook.Character.Name, lastField);
            emb.WithImageUrl(characterWebhook.Character.AvatarUrl);
            emb.WithFooter($"Created by {character.AuthorName}");

            return emb.Build();
        }

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

        public static async Task TryToReportInLogsChannel(DiscordSocketClient client, string text)
        {
            if (ConfigFile.DiscordLogsChannelID.Value.IsEmpty()) return;

            try
            {
                ulong channelId = ulong.Parse(ConfigFile.DiscordLogsChannelID.Value!);
                var channel = await client.GetChannelAsync(channelId) as SocketTextChannel;
                if (channel is null) return;

                await channel.SendMessageAsync(embed: text.ToInlineEmbed(Color.Orange));
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

        [GeneratedRegex("\\<(.*?)\\>")]
        private static partial Regex MentionRegex();
    }
}
