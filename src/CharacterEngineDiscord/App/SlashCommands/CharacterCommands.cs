using CharacterEngine.App.Handlers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Local;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using SakuraAi.Client;

namespace CharacterEngine.App.SlashCommands;


public class CharacterCommands : InteractionModuleBase<InteractionContext>
{
    public required DiscordSocketClient DiscordClient { get; set; }
    public required SakuraAiClient SakuraAiClient { get; set; }
    public required InteractionsHandler InteractionsHandler { get; set; }
    public required LocalStorage LocalStorage { get; set; }


    [SlashCommand("spawn-character", "Spawn new character!")]
    public async Task SpawnCharacter(string query, Enums.IntegrationType integrationType)
    {
        await RespondAsync(embed: MessagesTemplates.WAIT_MESSAGE);

        var characters = integrationType switch
        {
            Enums.IntegrationType.SakuraAi => await SakuraAiClient.SearchAsync(query),
            Enums.IntegrationType.CharacterAI => [],
            _ => throw new ArgumentOutOfRangeException(nameof(integrationType), integrationType, null)
        };

        if (characters.Count == 0)
        {
            await ModifyOriginalResponseAsync(msg => msg.Embed = $"No characters were found by query **\"{query}\"**".ToInlineEmbed(Color.Orange, false));
            return;
        }

        var searchQuery = new SearchQuery(Context.Channel.Id, Context.User.Id, query, characters.ToCommonCharacters(), integrationType);
        LocalStorage.SearchQueries.Add(searchQuery);

        await ModifyOriginalResponseAsync(msg => { msg.Embed = DiscordInteractionsHelper.BuildSearchResultList(searchQuery); msg.Components = DiscordInteractionsHelper.BuildSelectButtons(searchQuery.Pages > 1); });
    }
}
