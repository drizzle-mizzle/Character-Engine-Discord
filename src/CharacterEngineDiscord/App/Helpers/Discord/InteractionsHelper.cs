using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Local;
using Discord;

namespace CharacterEngine.App.Helpers.Discord;


public static class InteractionsHelper
{
    public const string SEP = "~sep~";


    public static SlashCommandProperties BuildStartCommand()
        => new SlashCommandBuilder().WithName("start").WithDescription("Register bot commands").WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild).Build();

    public static SlashCommandProperties BuildDisableCommand()
        => new SlashCommandBuilder().WithName("disable").WithDescription("Unregister all bot commands").WithDefaultMemberPermissions(GuildPermission.Administrator | GuildPermission.ManageGuild).Build();


    public static string NewCustomId(ModalActionType action, string data)
        => NewCustomId(Guid.NewGuid(), action, data);

    public static string NewCustomId(ModalData modalData)
        => NewCustomId(modalData.Id, modalData.ActionType, modalData.Data);

    public static string NewCustomId(Guid id, ModalActionType action, string data)
        => $"{id}{SEP}{action}{SEP}{data}";


    public static ModalData ParseCustomId(string customId)
    {
        var parts = customId.Split(SEP);
        return new ModalData(Guid.Parse(parts[0]), Enum.Parse<ModalActionType>(parts[1]), parts[2]);
    }


    public static Embed BuildSearchResultList(SearchQuery searchQuery)
    {
        (Color color, string iconEmoji) custom = searchQuery.IntegrationType switch
        {
            IntegrationType.SakuraAi => (Color.Purple, MessagesTemplates.SAKURA_EMOJI),
            IntegrationType.CharacterAI => (Color.Blue, "")
        };

        var embed = new EmbedBuilder();
        embed.WithColor(custom.color);
        embed.AddField($"{custom.iconEmoji} {searchQuery.IntegrationType:G}",
                       $"({searchQuery.Characters.Count}) Characters found by query **\"{searchQuery.OriginalQuery}\"**:");

        var rows = Math.Min(searchQuery.Characters.Count, 10);
        for (var row = 1; row <= rows; row++)
        {
            var characterIndex = row + (searchQuery.CurrentPage - 1) * 10;
            var character = searchQuery.Characters.ElementAt(characterIndex);

            var titleLine = $"{characterIndex}. {character.CharacterName}";
            var descLine = $"{character.Stat} | Author: {character.Author}";
            if (searchQuery.CurrentRow == row)
            {
                titleLine += " - ✅";
            }

            embed.AddField(titleLine, descLine);
        }

        embed.WithFooter($"Page {searchQuery.CurrentPage}/{searchQuery.Pages}");
        return embed.Build();
    }




}
