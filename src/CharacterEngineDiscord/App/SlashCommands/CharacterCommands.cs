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
    private readonly IServiceProvider _serviceProvider;
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public CharacterCommands(IServiceProvider serviceProvider, AppDbContext db, DiscordSocketClient discordClient, InteractionService interactions)
    {
        _serviceProvider = serviceProvider;
        _db = db;

        _discordClient = discordClient;
        _interactions = interactions;
    }


    [SlashCommand("spawn-character", "Spawn new character!")]
    public async Task SpawnCharacter(string query, IntegrationType integrationType)
    {
        await RespondAsync(embed: MessagesTemplates.WAIT_MESSAGE);

        var characters = await (integrationType switch
        {
            IntegrationType.SakuraAI => RuntimeStorage.SakuraAiModule.SearchAsync(query),
            // IntegrationType.CharacterAI =>
        });

        if (characters.Count == 0)
        {
            await ModifyOriginalResponseAsync(msg => { msg.Embed = $"{integrationType.GetIcon()} No characters were found by query **\"{query}\"**".ToInlineEmbed(Color.Orange, false); });
            return;
        }

        var searchQuery = new SearchQuery(Context.Channel.Id, Context.User.Id, query, characters, integrationType);
        RuntimeStorage.SearchQueries.Add(searchQuery);

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = InteractionsHelper.BuildSearchResultList(searchQuery);
            msg.Components = ButtonsHelper.BuildSearchButtons(searchQuery.Pages > 1);
        });
    }
}
