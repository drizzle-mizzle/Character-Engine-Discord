using CharacterEngine.Api.Abstractions;
using CharacterEngine.Helpers.Discord;
using CharacterEngine.Helpers.Integrations;
using CharacterEngine.Models;
using static CharacterEngine.Models.Enums;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using SakuraAi;


namespace CharacterEngine.Api.SlashCommandsHandlers;


public class CharacterCommands : InteractionModuleBase<InteractionContext>
{
    public required DiscordSocketClient DiscordClient { get; set; }
    public required SakuraAiClient SakuraAiClient { get; set; }
    public required InteractionsHandler InteractionsHandler { get; set; }


    [SlashCommand("spawn-character", "Spawn new character!")]
    public async Task SpawnCharacter(string query, IntegrationType integrationType)
    {
        await RespondAsync(embed: MessagesTemplates.WAIT_MESSAGE);

        var characters = integrationType switch
        {
            IntegrationType.SakuraAi => await SakuraAiClient.SearchAsync(query),
            IntegrationType.CharacterAI => [],
            _ => throw new ArgumentOutOfRangeException(nameof(integrationType), integrationType, null)
        };

        if (characters.Count == 0)
        {
            await ModifyOriginalResponseAsync(msg => msg.Embed = $"No characters were found by query **\"{query}\"**".ToInlineEmbed(Color.Orange, false));
            return;
        }

        var searchQuery = new SearchQuery(Context.Channel.Id, Context.User.Id, query, characters.ToCommonCharacters(), integrationType);
        lock (HandlerBase.SearchQueries)
        {
            HandlerBase.SearchQueries.Add(searchQuery);
        }

        await ModifyOriginalResponseAsync(msg => { msg.Embed = DiscordCommandsHelper.BuildSearchResultList(searchQuery); msg.Components = DiscordCommandsHelper.BuildSelectButtons(searchQuery.Pages > 1); });
    }
}
