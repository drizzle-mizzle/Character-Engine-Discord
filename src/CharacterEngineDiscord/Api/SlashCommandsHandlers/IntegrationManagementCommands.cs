using CharacterEngine.Database;
using CharacterEngine.Helpers;
using CharacterEngine.Helpers.Discord;
using CharacterEngine.Models;
using CharacterEngine.Models.Db;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using SakuraAi;
using static CharacterEngine.Models.Enums;

namespace CharacterEngine.Api.SlashCommandsHandlers;


[Group("integration", "Integrations Management")]
public class IntegrationManagementCommands : InteractionModuleBase<InteractionContext>
{
    public required DiscordSocketClient DiscordClient { get; set; }
    public required SakuraAiClient SakuraAiClient { get; set; }
    public required AppDbContext db { get; set; }


    [SlashCommand("create", "Create new integration for this guild")]
    public async Task Create(Enums.IntegrationType type)
    {
        var customId = DiscordModalsHelper.NewCustomId(ModalActionType.CreateIntegration, type.ToString("D"));
        var modalBuilder = new ModalBuilder().WithTitle($"Create {type:G} integration").WithCustomId(customId);

        var modal = type switch
        {
            IntegrationType.SakuraAi => modalBuilder.BuildSakuraAiAuthModal(),
            IntegrationType.CharacterAI => throw new NotImplementedException(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        await RespondWithModalAsync(modal).ConfigureAwait(false);
    }




}
