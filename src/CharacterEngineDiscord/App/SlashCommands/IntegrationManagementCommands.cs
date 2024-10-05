using CharacterEngine.App.Helpers.Common;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using SakuraAi.Client;
using SakuraAi.Client.Exceptions;
using SakuraAi.Client.Models.Common;

namespace CharacterEngine.App.SlashCommands;


[Group("integration", "Integrations Management")]
public class IntegrationManagementCommands : InteractionModuleBase<InteractionContext>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppDbContext _db;
    private readonly LocalStorage _localStorage;
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;
    private readonly SakuraAiClient _sakuraAiClient;


    public IntegrationManagementCommands(IServiceProvider serviceProvider, AppDbContext db, LocalStorage localStorage,
                                         DiscordSocketClient discordClient, InteractionService interactions, SakuraAiClient sakuraAiClient)
    {
        _serviceProvider = serviceProvider;
        _db = db;

        _localStorage = localStorage;
        _discordClient = discordClient;
        _interactions = interactions;
        _sakuraAiClient = sakuraAiClient;
    }


    [SlashCommand("create", "Create new integration for this guild")]
    public async Task Create(IntegrationType type)
    {
        var customId = InteractionsHelper.NewCustomId(ModalActionType.CreateIntegration, $"{type:D}");
        var modalBuilder = new ModalBuilder().WithTitle($"Create {type:G} integration").WithCustomId(customId);

        var modal = type switch
        {
            IntegrationType.SakuraAi => modalBuilder.BuildSakuraAiAuthModal(),
            IntegrationType.CharacterAI => throw new NotImplementedException(),
        };

        await RespondWithModalAsync(modal).ConfigureAwait(false);
    }

}
