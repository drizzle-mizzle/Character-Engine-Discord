using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Models.Database;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using CharacterEngineDiscord.Models.Common;
using Discord.WebSocket;

namespace CharacterEngineDiscord.Services
{
    internal static class CommandsService
    {
        internal static bool IsHoster(this SocketGuildUser? user)
            => user is not null && user.Id == ulong.Parse(ConfigFile.HosterDiscordID.Value!);

        internal static bool IsServerOwner(this SocketGuildUser? user)
            => user is not null && user.Id == user.Guild.OwnerId;

        internal static bool IsCharManager(this SocketGuildUser? user)
            => user is not null && user.Roles.Any(r => r.Name == ConfigFile.DiscordBotRole.Value);

        internal static async Task SendNoPowerFileAsync(this InteractionContext context)
        {
            var filename = ConfigFile.NoPermissionFile.Value;
            if (filename is null) return;

            var stream = File.OpenRead($"{EXE_DIR}storage{SC}{filename}");
            await context.Interaction.RespondWithFileAsync(stream, filename);
        }

        internal static Embed SpawnCharacterEmbed(CharacterWebhook webhook, Models.Database.Character character)
        {
            var embed = new EmbedBuilder()
                .WithTitle($"{OK_SIGN_DISCORD} **Success**")
                .WithColor(Color.Gold)
                .WithDescription($"Use *`\"{webhook.CallPrefix}\"`* prefix or replies to call the character.")
                .WithImageUrl(webhook.Character.AvatarUrl)
                .WithFooter($"Created by {character.AuthorName}")
                .AddField("Usage example:", $"*`{webhook.CallPrefix}hey!`*") 
                .AddField("Configuration", $"Webhook ID: *`{webhook.Id}`*\nUse it to modify this integration with *`/update-character`* command.")
                .AddField(webhook.Character.Name, $"*\"{character.Title}\"*\n\n{character.Description}\n\n" +
                                              (webhook.IntegrationType is IntegrationType.CharacterAI ? $"*Original link: [Chat with {character.Name}](https://beta.character.ai/chat?char={character.Id})\n" : "") +
                                              $"Can generate images: {(character.ImageGenEnabled is true ? "Yes" : "No")}\n" +
                                              (webhook.IntegrationType is IntegrationType.CharacterAI ? $"Interactions: {character.Interactions}*" : $"Stars: {character.Stars}"));
            return embed.Build();
        }

        internal static async Task<SearchQuery?> BuildAndSendSelectionMenuAsync(InteractionContext context, SearchQueryData searchQueryData)
        {
            if (!searchQueryData.IsSuccessful)
            {
                await context.Interaction.ModifyOriginalResponseAsync(msg => msg.Embed = InlineEmbed($"{WARN_SIGN_DISCORD} Failed to find a character: `{searchQueryData.ErrorReason}`", Color.Red));
                return null;
            }

            if (searchQueryData.IsEmpty)
            {
                await context.Interaction.ModifyOriginalResponseAsync(msg => msg.Embed = InlineEmbed($"{WARN_SIGN_DISCORD} No characters were found", Color.Orange));
                return null;
            }

            int pages = (int)Math.Ceiling(searchQueryData.Characters.Count / 10.0f);
            var query = new SearchQuery((ulong)context.Interaction.ChannelId!, context.User.Id, searchQueryData, pages);
            var list = BuildCharactersList(query);
            var buttons = BuildSelectButtons(query);
            await context.Interaction.ModifyOriginalResponseAsync(msg => { msg.Embed = list; msg.Components = buttons; });
            // Further logic will be handled by the ButtonsAndReactionsHandler

            return query;
        }

        public static Embed BuildCharactersList(SearchQuery query)
        {
            var list = new EmbedBuilder()
                .WithTitle($"Characters found by query \"{query.SearchQueryData.OriginalQuery}\":\n({query.SearchQueryData.Characters.Count})\n")
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

                if (i + 1 == query.CurrentRow)
                    fTitle += " - ✅";
                string interactionsOrStars;
                if (query.SearchQueryData.IntegrationType is IntegrationType.CharacterAI)
                    interactionsOrStars = $"Interactions: {character.Interactions}";
                else
                    interactionsOrStars = $"Stars: {character.Stars}";

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

        
    }
}
