using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.SlashCommands;


[ValidateAccessLevel(AccessLevels.Manager)]
[ValidateChannelPermissions]
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


    [SlashCommand("remove", "Remove an integration")]
    public async Task Remove(IntegrationType type, bool removeAssociatedCharacters)
    {
        await DeferAsync();

        IGuildIntegration? integration = await (type switch
        {
            IntegrationType.SakuraAI => _db.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == Context.Guild.Id)
        });

        if (integration is null)
        {
             await FollowupAsync(embed: $"There's no {type:G} integration on this server".ToInlineEmbed(Color.Orange));
             return;
        }

        await DatabaseHelper.DeleteGuildIntegrationAsync(integration, removeAssociatedCharacters);
        // TODO: delete webhooks

        await FollowupAsync(embed: $"{type:G} integration {(removeAssociatedCharacters ? "and all associated characters were" : "was")} successfully removed".ToInlineEmbed(Color.Green));
    }
}
