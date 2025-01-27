using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Domain.Models.Db.Integrations;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.SlashCommands;


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


    [ValidateAccessLevel(AccessLevels.Manager)]
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


    [ValidateAccessLevel(AccessLevels.Manager)]
    [SlashCommand("re-login", "Re-login into the integration")]
    public async Task ReLogin(IntegrationType type)
    {
        await DeferAsync(ephemeral: true);

        var guildIntegration = await DatabaseHelper.GetGuildIntegrationAsync(Context.Guild.Id, type);
        if (guildIntegration is null)
        {
            throw new UserFriendlyException("Integration not found");
        }

        await (guildIntegration switch
        {
            SakuraAiGuildIntegration sakuraAiGuildIntegration => InteractionsHelper.SendSakuraAiMailAsync(Context.Interaction, sakuraAiGuildIntegration.SakuraEmail),
            CaiGuildIntegration caiGuildIntegration => InteractionsHelper.SendCharacterAiMailAsync(Context.Interaction, caiGuildIntegration.CaiEmail),
            _ => throw new UserFriendlyException($"This command is not intended to be used with {type:G} integrations")
        });
    }


    [ValidateAccessLevel(AccessLevels.Manager)]
    [SlashCommand("copy", "Copy existing integration from another server")]
    public async Task Copy(string integrationId)
    {
        await DeferAsync();

        var guildIntegration = await DatabaseHelper.GetGuildIntegrationAsync(Guid.Parse(integrationId));
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
        var existingIntegration = await DatabaseHelper.GetGuildIntegrationAsync(Context.Guild.Id, type);
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
                MetricsWriter.Create(MetricType.IntegrationCreated, sakuraAiGuildIntegration.Id, $"{sakuraAiGuildIntegration.GetIntegrationType():G} | {sakuraAiGuildIntegration.SakuraEmail}");
                break;
            }
            case CaiGuildIntegration caiGuildIntegration:
            {
                _db.CaiIntegrations.Add(caiGuildIntegration);
                MetricsWriter.Create(MetricType.IntegrationCreated, caiGuildIntegration.Id, $"{caiGuildIntegration.GetIntegrationType():G} | {caiGuildIntegration.CaiEmail}");
                break;
            }
            case OpenRouterGuildIntegration openRouterGuildIntegration:
            {
                _db.OpenRouterIntegrations.Add(openRouterGuildIntegration);
                MetricsWriter.Create(MetricType.IntegrationCreated, openRouterGuildIntegration.Id, $"{openRouterGuildIntegration.GetIntegrationType():G} | {openRouterGuildIntegration.OpenRouterApiKey}");
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


    [ValidateAccessLevel(AccessLevels.Manager)]
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
                var caiUser = await MemoryStorage.IntegrationModules.CaiModule.LoginByLinkAsync(data);

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

                MetricsWriter.Create(MetricType.IntegrationCreated, newCaiIntergration.Id, $"{type:G} | {newCaiIntergration.CaiEmail}");

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


    [ValidateAccessLevel(AccessLevels.Manager)]
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
            var spawnedCharacters = await DatabaseHelper.GetAllSpawnedCharactersInChannelAsync(channel.Id);

            foreach (var spawnedCharacter in spawnedCharacters.Where(sc => sc.GetIntegrationType() == type))
            {

                if (removeAssociatedCharacters)
                {
                    var deleteSpawnedCharacterAsync = DatabaseHelper.DeleteSpawnedCharacterAsync(spawnedCharacter);

                    try
                    {
                        var webhookClient = MemoryStorage.CachedWebhookClients.Find(spawnedCharacter.WebhookId);
                        if (webhookClient is not null)
                        {
                            await webhookClient.DeleteWebhookAsync();
                        }
                    }
                    catch
                    {
                        // care not
                    }

                    MemoryStorage.CachedCharacters.Remove(spawnedCharacter.Id);
                    MemoryStorage.CachedWebhookClients.Remove(spawnedCharacter.WebhookId);
                    await deleteSpawnedCharacterAsync;
                }
                else
                {
                    var cachedCharacter = MemoryStorage.CachedCharacters.Find(spawnedCharacter.Id)!;
                    cachedCharacter.FreewillFactor = spawnedCharacter.FreewillFactor;

                    spawnedCharacter.FreewillFactor = 0;
                    await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);
                }
            }
        }

        await DatabaseHelper.DeleteGuildIntegrationAsync(integration);


        await FollowupAsync(embed: $"{type.GetIcon()} {type:G} integration {(removeAssociatedCharacters ? "and all associated characters were" : "was")} successfully removed".ToInlineEmbed(Color.Green));
    }
}
