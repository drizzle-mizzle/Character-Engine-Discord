using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Local;
using Discord;

namespace CharacterEngine.App.Helpers.Discord;


public static class DiscordInteractionsHelper
{
    public static SlashCommandProperties BuildStartCommand()
        => new SlashCommandBuilder().WithName("start").WithDescription("Register bot commands").WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild).Build();


    public static SlashCommandProperties BuildDisableCommand()
        => new SlashCommandBuilder().WithName("disable").WithDescription("Unregister all bot commands").WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild).Build();


    public static Embed BuildSearchResultList(SearchQuery searchQuery)
    {
        (Color color, string iconEmoji) custom = searchQuery.IntegrationType switch
        {
            IntegrationType.SakuraAi => (Color.Purple, MessagesTemplates.SAKURA_EMOJI),
            IntegrationType.CharacterAI => (Color.Blue, "")
        };

        var embed = new EmbedBuilder().WithColor(custom.color)
                                      .WithFooter($"Page {searchQuery.CurrentPage}/{searchQuery.Pages}");

        embed.AddField($"{custom.iconEmoji} {searchQuery.IntegrationType:G}", $"({searchQuery.Characters.Count}) Characters found by query **\"{searchQuery.OriginalQuery}\"**:");

        for (int index = 1; index <= Math.Min(searchQuery.Characters.Count, 10); index++)
        {
            var num = (searchQuery.CurrentPage - 1) * 10 + index;
            var character = searchQuery.Characters.ElementAt(num);
            embed.AddField($"{num}. {character.CharacterName}" + (index == searchQuery.CurrentRow ? " - ✅" : ""),
                           $"{character.Stat} | Author: {character.Author}");
        }

        return embed.Build();
    }


    public static MessageComponent BuildSelectButtons(bool withPages)
    {
        // List navigation buttons
        var buttons = new ComponentBuilder().WithButton(emote: new Emoji("\u2B06"), customId: "up", style: ButtonStyle.Secondary)
                                            .WithButton(emote: new Emoji("\u2B07"), customId: "down", style: ButtonStyle.Secondary)
                                            .WithButton(emote: new Emoji("\u2705"), customId: "select", style: ButtonStyle.Success);
        // Pages navigation buttons
        if (withPages)
        {
            buttons.WithButton(emote: new Emoji("\u2B05"), customId: "left", row: 1)
                   .WithButton(emote: new Emoji("\u27A1"), customId: "right", row: 1);
        }

        return buttons.Build();
    }

}
