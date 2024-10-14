using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands;


[Group("integration", "Integrations Management")]
public class IntegrationManagementCommands : InteractionModuleBase<InteractionContext>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public IntegrationManagementCommands(IServiceProvider serviceProvider, AppDbContext db, DiscordSocketClient discordClient, InteractionService interactions)
    {
        _serviceProvider = serviceProvider;
        _db = db;

        _discordClient = discordClient;
        _interactions = interactions;
    }


    [SlashCommand("create", "Create new integration for this guild")]
    public async Task Create(IntegrationType type)
    {
        var customId = InteractionsHelper.NewCustomId(ModalActionType.CreateIntegration, $"{type:D}");
        var modalBuilder = new ModalBuilder().WithTitle($"Create {type:G} integration").WithCustomId(customId);

        var modal = type switch
        {
            IntegrationType.SakuraAI => modalBuilder.BuildSakuraAiAuthModal(),
            IntegrationType.CharacterAI => throw new NotImplementedException(),
        };

        await RespondWithModalAsync(modal).ConfigureAwait(false);
    }

}
