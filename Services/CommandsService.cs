using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Database;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;

namespace CharacterEngineDiscord.Services
{
    internal static class CommandsService
    {
        internal static Embed SpawnCharacterEmbed(CharacterWebhook webhook, Models.Database.Character character)
        {
            var embed = new EmbedBuilder()
            {
                Title = $"{OK_SIGN_DISCORD} **Success**",
                Color = Color.Gold,
                Description = $"Use *`\"{webhook.CallPrefix}\"`* prefix or replies to call the character.",
                ImageUrl = webhook.Character.AvatarUrl
            }.AddField("Usage example:", $"*`{webhook.CallPrefix}hey!`*")
             .AddField("Configuration", $"Webhook ID: *`{webhook.Id}`*\nUse it to modify this integration with *`/update-character`* command.")
             .AddField(webhook.Character.Name, $"*\"{character.Title}\"*\n\n" +
                                               $"{character.Description}\n\n" +
                                               $"*Original link: [Chat with {character.Name}](https://beta.character.ai/chat?char={character.Id})\n" +
                                               $"Can generate images: {(character.ImageGenEnabled is true ? "Yes" : "No")}\n" +
                                               $"Interactions: {character.Interactions}*")
             .WithFooter($"Created by {character.AuthorName}");

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
                .WithColor(Color.Blue);

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

                list.AddField($"{index + 1}. {fTitle}", $"Interactions: {character.Interactions} | Author: {character.AuthorName}");
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
