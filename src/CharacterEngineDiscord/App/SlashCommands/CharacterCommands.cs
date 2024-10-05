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
    private readonly IServiceProvider _serviceProvider;
    private readonly AppDbContext _db;
    private readonly LocalStorage _localStorage;
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;
    private readonly SakuraAiClient _sakuraAiClient;


    public CharacterCommands(IServiceProvider serviceProvider, AppDbContext db, LocalStorage localStorage, DiscordSocketClient discordClient, InteractionService interactions, SakuraAiClient sakuraAiClient)
    {
        _serviceProvider = serviceProvider;
        _db = db;

        _localStorage = localStorage;
        _discordClient = discordClient;
        _interactions = interactions;
        _sakuraAiClient = sakuraAiClient;
    }


    [SlashCommand("spawn-character", "Spawn new character!")]
    public async Task SpawnCharacter(string query, IntegrationType integrationType)
    {
        await RespondAsync(embed: MessagesTemplates.WAIT_MESSAGE);

        var characters = integrationType switch
        {
            IntegrationType.SakuraAi => await _sakuraAiClient.SearchAsync(query),
            IntegrationType.CharacterAI => []
        };

        if (characters.Count == 0)
        {
            await ModifyOriginalResponseAsync(msg => { msg.Embed = $"No characters were found by query **\"{query}\"**".ToInlineEmbed(Color.Orange, false); });
            return;
        }

        var searchQuery = new SearchQuery(Context.Channel.Id, Context.User.Id, query, characters.AsCommonCharacters(), integrationType);
        _localStorage.SearchQueries.Add(searchQuery);

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = InteractionsHelper.BuildSearchResultList(searchQuery);
            msg.Components = ButtonsHelper.BuildSelectButtons(searchQuery.Pages > 1);
        });
    }
}
