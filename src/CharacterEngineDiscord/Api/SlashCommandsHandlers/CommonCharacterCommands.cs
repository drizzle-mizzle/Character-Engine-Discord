using CharacterEngine.Helpers;
using CharacterEngine.Helpers.Discord;
using CharacterEngine.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using SakuraAi;

namespace CharacterEngine.Api.SlashCommandsHandlers;


public class CommonCharacterCommands : InteractionModuleBase<InteractionContext>
{
    public required DiscordSocketClient DiscordClient { get; set; }
    public required SakuraAiClient SakuraAiClient { get; set; }


    [SlashCommand("spawn-character", "Spawn new character!")]
    public async Task SpawnCharacter(string query, Enums.IntegrationType integrationType)
    {
        await RespondAsync(embed: MessagesTemplates.WAIT_MESSAGE);
        var characters = await SakuraAiClient.SearchAsync(query);

        var embed = new EmbedBuilder().WithTitle($"({characters.Count}) Characters found by query \"{query}\" :")
                                     // .WithFooter($"Page {query.CurrentPage}/{query.Pages}")
                                     .WithColor(Color.Green)
                                     .WithFooter(integrationType.ToString("G"));

        var l = Math.Min(characters.Count, 10);
        for (int index = 0; index < l; index ++)
        {
            var character = characters.ElementAt(index);
            embed.AddField($"{index + 1}. {character.name}", $"{character.messageCount} | Author: {character.creatorUsername}");
        }

        await ModifyOriginalResponseAsync(msg => { msg.Embed = embed.Build(); });
    }
}
