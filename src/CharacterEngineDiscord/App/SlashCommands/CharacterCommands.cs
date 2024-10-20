using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Helpers.Integrations;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands;


public class CharacterCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;


    public CharacterCommands(AppDbContext db, DiscordSocketClient discordClient)
    {
        _db = db;
        _discordClient = discordClient;
    }


    [SlashCommand("spawn-character", "Spawn new character!")]
    public async Task SpawnCharacter(string query, IntegrationType integrationType)
    {
        await RespondAsync(embed: MessagesTemplates.WAIT_MESSAGE);

        var module = integrationType.GetIntegrationModule();
        var characters = await module.SearchAsync(query);

        if (characters.Count == 0)
        {
            await ModifyOriginalResponseAsync(msg => { msg.Embed = $"{integrationType.GetIcon()} No characters were found by query **\"{query}\"**".ToInlineEmbed(Color.Orange, false); });
            return;
        }

        var searchQuery = new SearchQuery(Context.Channel.Id, Context.User.Id, query, characters, integrationType);
        StaticStorage.SearchQueries.Add(searchQuery);

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = InteractionsHelper.BuildSearchResultList(searchQuery);
            msg.Components = ButtonsHelper.BuildSearchButtons(searchQuery.Pages > 1);
        });
    }
}
