using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands;


[ValidateAccessLevel(AccessLevels.Manager)]
[Group("integration", "Integrations Management")]
public class IntegrationManagementCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;


    public IntegrationManagementCommands(AppDbContext db, DiscordSocketClient discordClient)
    {
        _db = db;
        _discordClient = discordClient;
    }


    [SlashCommand("create", "Create new integration for this guild")]
    public async Task Create(IntegrationType type)
    {
        var customId = InteractionsHelper.NewCustomId(ModalActionType.CreateIntegration, $"{type:D}");
        var modalBuilder = new ModalBuilder().WithTitle($"Create {type:G} integration").WithCustomId(customId);

        var modal = type switch
        {
            IntegrationType.SakuraAI => modalBuilder.BuildSakuraAiAuthModal()

        };

        await RespondWithModalAsync(modal);
    }

}
