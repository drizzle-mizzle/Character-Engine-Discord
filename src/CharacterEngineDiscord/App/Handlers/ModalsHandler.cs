using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Masters;
using CharacterEngine.App.Repositories;
using CharacterEngine.App.Services;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Domain.Models.Db.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Models;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace CharacterEngine.App.Handlers;


public class ModalsHandler
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;
    private readonly IntegrationsDbRepository _integrationsDbRepository;
    private readonly IntegrationsMaster _integrationsMaster;

    // private readonly CharactersRepository _charactersRepository;
    // private readonly CacheRepository _cacheRepository;


    public ModalsHandler(
        AppDbContext db,
        DiscordSocketClient discordClient,
        IntegrationsDbRepository integrationsDbRepository,
        IntegrationsMaster integrationsMaster
        // CharactersRepository charactersRepository,
        // CacheRepository cacheRepository
    )
    {
        _db = db;
        _discordClient = discordClient;
        _integrationsDbRepository = integrationsDbRepository;
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
            ModalActionType.CreateIntegration => CreateIntegrationAsync(modal, int.Parse(parsedModal.Data)),
            ModalActionType.OpenRouterSettings => UpdateOpenRouterSettingsAsync(modal, parsedModal)
        });
    }


    private async Task UpdateOpenRouterSettingsAsync(SocketModal modal, ModalData parsedModal)
    {
        var splitData = parsedModal.Data.Split('~');
        var id = Guid.Parse(splitData[0]);
        var settingTarget = Enum.Parse<SettingTarget>(splitData[1]);
        var jsonSettings = modal.Data.Components.First(c => c.CustomId == "settings").Value;
        var settings = JsonConvert.DeserializeObject<OpenRouterSettings>(jsonSettings);
        ArgumentNullException.ThrowIfNull(settings);

        string target;
        switch (settingTarget)
        {
            case SettingTarget.Guild:
            {
                var guildIntegration = await _db.OpenRouterIntegrations.FindAsync(id);
                ArgumentNullException.ThrowIfNull(guildIntegration);

                guildIntegration.OpenRouterModel = settings.OpenRouterModel;
                guildIntegration.OpenRouterTemperature = settings.OpenRouterTemperature;
                guildIntegration.OpenRouterTopP = settings.OpenRouterTopP;
                guildIntegration.OpenRouterTopK = settings.OpenRouterTopK;
                guildIntegration.OpenRouterFrequencyPenalty = settings.OpenRouterFrequencyPenalty;
                guildIntegration.OpenRouterPresencePenalty = settings.OpenRouterPresencePenalty;
                guildIntegration.OpenRouterRepetitionPenalty = settings.OpenRouterRepetitionPenalty;
                guildIntegration.OpenRouterMinP = settings.OpenRouterMinP;
                guildIntegration.OpenRouterTopA = settings.OpenRouterTopA;
                guildIntegration.OpenRouterMaxTokens = settings.OpenRouterMaxTokens;

                target = "current server";

                break;
            }
            case SettingTarget.Character:
            {
                var spawnedCharacter = await _db.OpenRouterSpawnedCharacters.FindAsync(id);
                ArgumentNullException.ThrowIfNull(spawnedCharacter);

                spawnedCharacter.OpenRouterModel = settings.OpenRouterModel;
                spawnedCharacter.OpenRouterTemperature = settings.OpenRouterTemperature;
                spawnedCharacter.OpenRouterTopP = settings.OpenRouterTopP;
                spawnedCharacter.OpenRouterTopK = settings.OpenRouterTopK;
                spawnedCharacter.OpenRouterFrequencyPenalty = settings.OpenRouterFrequencyPenalty;
                spawnedCharacter.OpenRouterPresencePenalty = settings.OpenRouterPresencePenalty;
                spawnedCharacter.OpenRouterRepetitionPenalty = settings.OpenRouterRepetitionPenalty;
                spawnedCharacter.OpenRouterMinP = settings.OpenRouterMinP;
                spawnedCharacter.OpenRouterTopA = settings.OpenRouterTopA;
                spawnedCharacter.OpenRouterMaxTokens = settings.OpenRouterMaxTokens;

                target = $"character {spawnedCharacter.GetMention()}";

                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        await _db.SaveChangesAsync();

        var embed = new EmbedBuilder().WithTitle($"{IntegrationType.OpenRouter.GetIcon()} OpenRouter settings for {target} were updated successfully.")
                                      .WithColor(IntegrationType.OpenRouter.GetColor())
                                      .WithDescription($"```json\n{jsonSettings}\n```");

        await modal.FollowupAsync(embed: embed.Build());
    }


    private async Task CreateIntegrationAsync(SocketModal modal, int intergrationType)
    {
        var type = (IntegrationType)intergrationType;
        var existingIntegration = await _integrationsDbRepository.GetGuildIntegrationAsync((ulong)modal.GuildId!, type);
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

        MetricsWriter.Write(MetricType.IntegrationCreated, newIntegration.Id, $"{IntegrationType.OpenRouter:G}");

        var embed = new EmbedBuilder().WithTitle($"{type.GetIcon()} {type:G} API key registered")
                                      .WithColor(IntegrationType.CharacterAI.GetColor())
                                      .WithDescription($"From now on, this API key will be used for all {type:G} interactions on this server.\n{type.GetNextStepTail()}");

        await modal.FollowupAsync(embed: embed.Build());
    }

}
