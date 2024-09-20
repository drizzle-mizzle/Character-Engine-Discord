using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using SakuraAi.Client;

namespace CharacterEngine.App.SlashCommands;


[Group("integration", "Integrations Management")]
public class IntegrationManagementCommands : InteractionModuleBase<InteractionContext>
{
    public required DiscordSocketClient DiscordClient { get; set; }
    public required SakuraAiClient SakuraAiClient { get; set; }
    public required AppDbContext db { get; set; }


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
