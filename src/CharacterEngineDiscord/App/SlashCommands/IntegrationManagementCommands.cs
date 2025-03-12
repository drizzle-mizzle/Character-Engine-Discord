using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Masters;
using CharacterEngine.App.Infrastructure;
using CharacterEngine.App.Repositories;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Domain.Models.Db.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Helpers;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using static CharacterEngine.App.Helpers.Discord.ValidationsHelper;

namespace CharacterEngine.App.SlashCommands;


[ValidateChannelPermissions]
[Group("integration", "Integrations Management")]
public class IntegrationManagementCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly IntegrationsRepository _integrationsRepository;
    private readonly CharactersRepository _charactersRepository;
    private readonly IntegrationsMaster _integrationsMaster;
    private readonly CacheRepository _cacheRepository;


    public IntegrationManagementCommands(
        AppDbContext db,
        IntegrationsRepository integrationsRepository,
        CharactersRepository charactersRepository,
        IntegrationsMaster integrationsMaster,
        CacheRepository cacheRepository
    )
    {
        _db = db;
        _integrationsRepository = integrationsRepository;
        _charactersRepository = charactersRepository;
        _integrationsMaster = integrationsMaster;
        _cacheRepository = cacheRepository;
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("create", "Create new integration for this server")]
    public async Task Create(IntegrationType type)
    {
        var customId = InteractionsHelper.NewCustomId(ModalActionType.CreateIntegration, $"{type:D}");
        var modalBuilder = new ModalBuilder().WithTitle($"Create {type:G} integration").WithCustomId(customId);

        var modal = type switch
        {
            IntegrationType.SakuraAI => modalBuilder.BuildSakuraAiAuthModal(),
            IntegrationType.CharacterAI => modalBuilder.BuildCaiAiAuthModal(),
            IntegrationType.OpenRouter => modalBuilder.BuildOpenRouterAuthModal(),
        };

        await RespondWithModalAsync(modal); // next in EnsureSakuraAiLoginAsync()
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("re-login", "Re-login into the integration")]
    public async Task ReLogin(IntegrationType type)
    {
        await DeferAsync(ephemeral: true);

        var guildIntegration = await _integrationsRepository.GetGuildIntegrationAsync(Context.Guild.Id, type);
        if (guildIntegration is null)
        {
            throw new UserFriendlyException("Integration not found");
        }

        await (guildIntegration switch
        {
            SakuraAiGuildIntegration sakuraAiGuildIntegration => _integrationsMaster.SendSakuraAiMailAsync(Context.Interaction, sakuraAiGuildIntegration.SakuraEmail),
            CaiGuildIntegration caiGuildIntegration => _integrationsMaster.SendCharacterAiMailAsync(Context.Interaction, caiGuildIntegration.CaiEmail),
            _ => throw new UserFriendlyException($"This command is not intended to be used with {type:G} integrations")
        });
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("copy", "Copy existing integration from another server")]
    public async Task Copy(string integrationId)
    {
        await DeferAsync();

        var guildIntegration = await _integrationsRepository.GetGuildIntegrationAsync(Guid.Parse(integrationId));
        if (guildIntegration is null)
        {
            throw new UserFriendlyException("Integration not found");
        }

        var originalIntegrationGuild = await _db.DiscordGuilds.FirstAsync(g => g.Id == guildIntegration.DiscordGuildId);

        var allowed = originalIntegrationGuild.OwnerId == Context.User.Id
                   || await _db.GuildBotManagers.Where(m => m.DiscordGuildId == originalIntegrationGuild.Id)
                                                .AnyAsync(m => m.DiscordUserOrRoleId == Context.User.Id);

        if (!allowed)
        {
            throw new UserFriendlyException("You're not allowed to copy integrations from this server");
        }

        var type = guildIntegration.GetIntegrationType();
        var existingIntegration = await _integrationsRepository.GetGuildIntegrationAsync(Context.Guild.Id, type);
        if (existingIntegration is not null)
        {
            throw new UserFriendlyException($"This server already has {type.GetIcon()}{type:G} integration");
        }

        var copyGuildIntegration = Activator.CreateInstance(guildIntegration.GetType());
        foreach (var prop in guildIntegration.GetType().GetProperties())
        {
            var propValue = prop.GetValue(guildIntegration, null);
            prop.SetValue(copyGuildIntegration, propValue, null);
        }

        var castedGuildIntegration = (IGuildIntegration)copyGuildIntegration!;
        castedGuildIntegration.Id = Guid.NewGuid();
        castedGuildIntegration.DiscordGuildId = Context.Guild.Id;

        switch (castedGuildIntegration)
        {
            case SakuraAiGuildIntegration sakuraAiGuildIntegration:
            {
                _db.SakuraAiIntegrations.Add(sakuraAiGuildIntegration);
                MetricsWriter.Write(MetricType.IntegrationCreated, sakuraAiGuildIntegration.Id, $"{sakuraAiGuildIntegration.GetIntegrationType():G} | {sakuraAiGuildIntegration.SakuraEmail}");
                break;
            }
            case CaiGuildIntegration caiGuildIntegration:
            {
                _db.CaiIntegrations.Add(caiGuildIntegration);
                MetricsWriter.Write(MetricType.IntegrationCreated, caiGuildIntegration.Id, $"{caiGuildIntegration.GetIntegrationType():G} | {caiGuildIntegration.CaiEmail}");
                break;
            }
            case OpenRouterGuildIntegration openRouterGuildIntegration:
            {
                _db.OpenRouterIntegrations.Add(openRouterGuildIntegration);
                MetricsWriter.Write(MetricType.IntegrationCreated, openRouterGuildIntegration.Id, $"{openRouterGuildIntegration.GetIntegrationType():G} | {openRouterGuildIntegration.OpenRouterApiKey}");
                break;
            }
            default:
            {
                throw new ArgumentException();
            }
        }

        await _db.SaveChangesAsync();

        var msg = $"**{type.GetIcon()} {type:G}** integration was copied successfully | New integration ID: **`{castedGuildIntegration.Id}`**";

        await FollowupAsync(embed: msg.ToInlineEmbed(type.GetColor(), bold: false));
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("confirm", "Confirm integration")]
    public async Task Confirm(IntegrationType type, string data)
    {
        await DeferAsync(ephemeral: true);

        string message;
        string? thumbnailUrl;

        switch (type)
        {
            case IntegrationType.CharacterAI:
            {
                var caiUser = await IntegrationsHub.CharacterAiModule.LoginByLinkAsync(data);

                var newCaiIntergration = new CaiGuildIntegration
                {
                    CaiAuthToken = caiUser.Token,
                    CaiUserId = caiUser.UserId,
                    CaiUsername = caiUser.Username,
                    DiscordGuildId = Context.Guild.Id,
                    CreatedAt = DateTime.Now,
                    CaiEmail = caiUser.UserEmail
                };

                _db.CaiIntegrations.Add(newCaiIntergration);
                await _db.SaveChangesAsync();

                MetricsWriter.Write(MetricType.IntegrationCreated, newCaiIntergration.Id, $"{type:G} | {newCaiIntergration.CaiEmail}");

                message = $"Username: **{caiUser.Username}**\n" +
                          "From now on, this account will be used for all CharacterAI interactions on this server.\n" +
                          type.GetNextStepTail();

                thumbnailUrl = caiUser.UserImageUrl;

                break;
            }
            default:
            {
                throw new UserFriendlyException($"This command is not intended to be used with {type:G} integrations");
            }
        }

        var embed = new EmbedBuilder().WithTitle($"{type.GetIcon()} {type:G} user authorized")
                   .WithDescription(message)
                   .WithColor(type.GetColor())
                   .WithThumbnailUrl(thumbnailUrl);

        await FollowupAsync(embed: embed.Build());
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("remove", "Remove integration from this server")]
    public async Task Remove(IntegrationType type, bool removeAssociatedCharacters)
    {
        await DeferAsync();

        IGuildIntegration? integration = (type switch
        {
            IntegrationType.SakuraAI => await _db.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == Context.Guild.Id),
            IntegrationType.CharacterAI => await _db.CaiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == Context.Guild.Id),
            IntegrationType.OpenRouter => await _db.OpenRouterIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == Context.Guild.Id),

            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        });

        if (integration is null)
        {
             await FollowupAsync(embed: $"There's no {type:G} integration on this server".ToInlineEmbed(Color.Orange));
             return;
        }

        var channelsInGuild = await _db.DiscordChannels.Where(c => c.DiscordGuildId == integration.DiscordGuildId).ToListAsync();

        foreach (var channel in channelsInGuild)
        {
            var spawnedCharacters = await _charactersRepository.GetAllSpawnedCharactersInChannelAsync(channel.Id);

            foreach (var spawnedCharacter in spawnedCharacters.Where(sc => sc.GetIntegrationType() == type))
            {

                if (removeAssociatedCharacters)
                {
                    var deleteSpawnedCharacterAsync = _charactersRepository.DeleteSpawnedCharacterAsync(spawnedCharacter.Id);

                    try
                    {
                        var webhookClient = _cacheRepository.CachedWebhookClients.Find(spawnedCharacter.WebhookId);
                        if (webhookClient is not null)
                        {
                            await webhookClient.DeleteWebhookAsync();
                        }
                    }
                    catch
                    {
                        // care not
                    }

                    _cacheRepository.CachedCharacters.Remove(spawnedCharacter.Id);
                    _cacheRepository.CachedWebhookClients.Remove(spawnedCharacter.WebhookId);
                    await deleteSpawnedCharacterAsync;
                }
                else
                {
                    var cachedCharacter = _cacheRepository.CachedCharacters.Find(spawnedCharacter.Id)!;
                    cachedCharacter.FreewillFactor = spawnedCharacter.FreewillFactor;

                    spawnedCharacter.FreewillFactor = 0;
                    await _charactersRepository.UpdateSpawnedCharacterAsync(spawnedCharacter);
                }
            }
        }

        await _integrationsRepository.DeleteGuildIntegrationAsync(integration);


        await FollowupAsync(embed: $"{type.GetIcon()} {type:G} integration {(removeAssociatedCharacters ? "and all associated characters were" : "was")} successfully removed".ToInlineEmbed(Color.Green));
    }
}
