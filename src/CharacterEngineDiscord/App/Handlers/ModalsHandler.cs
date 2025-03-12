using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Masters;
using CharacterEngine.App.Repositories;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Db.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Shared;
using Discord;
using Discord.WebSocket;

namespace CharacterEngine.App.Handlers;


public class ModalsHandler
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;
    private readonly IntegrationsRepository _integrationsRepository;
    private readonly IntegrationsMaster _integrationsMaster;

    // private readonly CharactersRepository _charactersRepository;
    // private readonly CacheRepository _cacheRepository;


    public ModalsHandler(
        AppDbContext db,
        DiscordSocketClient discordClient,
        IntegrationsRepository integrationsRepository,
        IntegrationsMaster integrationsMaster
        // CharactersRepository charactersRepository,
        // CacheRepository cacheRepository
    )
    {
        _db = db;
        _discordClient = discordClient;
        _integrationsRepository = integrationsRepository;
        _integrationsMaster = integrationsMaster;

        // _charactersRepository = charactersRepository;
        // _cacheRepository = cacheRepository;
    }


    public Task HandleModal(SocketModal modal)
    {
        Task.Run(async () =>
        {
            try
            {
                await HandleModalAsync(modal);
            }
            catch (Exception e)
            {
                var traceId = CommonHelper.NewTraceId();
                await _discordClient.ReportErrorAsync("HandleModal", null, e, traceId, writeMetric: true);
                await InteractionsHelper.RespondWithErrorAsync(modal, e, traceId);
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleModalAsync(SocketModal modal)
    {
        await modal.DeferAsync(ephemeral: true);

        var parsedModal = InteractionsHelper.ParseCustomId(modal.Data.CustomId);

        await (parsedModal.ActionType switch
        {
            ModalActionType.CreateIntegration => CreateIntegrationAsync(modal, int.Parse(parsedModal.Data))
        });
    }


    private async Task CreateIntegrationAsync(SocketModal modal, int intergrationType)
    {
        var type = (IntegrationType)intergrationType;
        var existingIntegration = await _integrationsRepository.GetGuildIntegrationAsync((ulong)modal.GuildId!, type);
        if (existingIntegration is not null)
        {
            throw new UserFriendlyException($"This server already has {type.GetIcon()}{type:G} integration");
        }

        await (type switch
        {
            IntegrationType.SakuraAI => CreateSakuraAiIntegrationAsync(modal),
            IntegrationType.CharacterAI => CreateCharacterAiIntegrationAsync(modal),
            IntegrationType.OpenRouter => CreateOpenRouterIntegrationAsync(modal),

            _ => throw new ArgumentOutOfRangeException()
        });
    }

    private Task CreateSakuraAiIntegrationAsync(SocketModal modal)
    {
        var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();
        return _integrationsMaster.SendSakuraAiMailAsync(modal, email);
    }


    private Task CreateCharacterAiIntegrationAsync(SocketModal modal)
    {
        var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();
        return _integrationsMaster.SendCharacterAiMailAsync(modal, email);
    }


    private async Task CreateOpenRouterIntegrationAsync(SocketModal modal)
    {
        const IntegrationType type = IntegrationType.OpenRouter;

        var apiKey = modal.Data.Components.First(c => c.CustomId == "api-key").Value.Trim('\n', ' ');

        if (!apiKey.StartsWith("sk-or"))
        {
            throw new UserFriendlyException("Wrong API key");
        }

        var model = modal.Data.Components.First(c => c.CustomId == "model").Value.Trim('\n', ' ');

        var newIntegration = new OpenRouterGuildIntegration
        {
            OpenRouterApiKey = apiKey,
            OpenRouterModel = model,
            DiscordGuildId = modal.GuildId!.Value,
            CreatedAt = DateTime.Now
        };

        _db.OpenRouterIntegrations.Add(newIntegration);
        await _db.SaveChangesAsync();

        var embed = new EmbedBuilder().WithTitle($"{type.GetIcon()} {type:G} API key registered")
                                      .WithColor(IntegrationType.CharacterAI.GetColor())
                                      .WithDescription($"From now on, this API key will be used for all {type:G} interactions on this server.\n{type.GetNextStepTail()}");

        await modal.FollowupAsync(embed: embed.Build());
    }

}
