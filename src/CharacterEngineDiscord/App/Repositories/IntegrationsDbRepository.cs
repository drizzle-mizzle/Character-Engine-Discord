using CharacterEngine.App.Repositories.Abstractions;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db.Integrations;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.Repositories;


public class IntegrationsDbRepository(AppDbContext db) : RepositoryBase(db)
{
    public async Task<IGuildIntegration?> GetGuildIntegrationAsync(Guid integrationId)
    {
        return await DB.SakuraAiIntegrations.FindAsync(integrationId) as IGuildIntegration
            ?? await DB.CaiIntegrations.FindAsync(integrationId) as IGuildIntegration
            ?? await DB.OpenRouterIntegrations.FindAsync(integrationId) as IGuildIntegration;
    }


    public async Task<IGuildIntegration?> GetGuildIntegrationAsync(ISpawnedCharacter spawnedCharacter)
    {
        var guildId = await DB.DiscordChannels
                              .Where(c => c.Id == spawnedCharacter.DiscordChannelId)
                              .Select(c => c.DiscordGuildId)
                              .FirstAsync();

        var type = spawnedCharacter.GetIntegrationType();
        IGuildIntegration? integration = type switch
        {
            IntegrationType.SakuraAI => await DB.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
            IntegrationType.CharacterAI => await DB.CaiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
            IntegrationType.OpenRouter => await DB.OpenRouterIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
        };

        return integration;
    }


    public async Task<IGuildIntegration?> GetGuildIntegrationAsync(ulong guildId, IntegrationType integrationType)
    {

        IGuildIntegration? integration = integrationType switch
        {
            IntegrationType.SakuraAI => await DB.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
            IntegrationType.CharacterAI => await DB.CaiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
            IntegrationType.OpenRouter => await DB.OpenRouterIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId),
        };

        return integration;
    }


    public async Task<List<IGuildIntegration>> GetAllIntegrationsInGuildAsync(ulong guildId)
    {
        var result = new List<IGuildIntegration>();


        var sakuraAiGuildIntegration = await DB.SakuraAiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId);
        if (sakuraAiGuildIntegration is not null)
        {
            result.Add(sakuraAiGuildIntegration);
        }

        var characterAiGuildIntegration = await DB.CaiIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId);
        if (characterAiGuildIntegration is not null)
        {
            result.Add(characterAiGuildIntegration);
        }

        var openrouterGuildIntegration = await DB.OpenRouterIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == guildId);
        if (openrouterGuildIntegration is not null)
        {
            result.Add(openrouterGuildIntegration);
        }

        return result;
    }


    public async Task DeleteGuildIntegrationAsync(IGuildIntegration guildIntegration)
    {
        switch (guildIntegration)
        {
            case SakuraAiGuildIntegration sakuraAiGuildIntegration:
            {
                DB.SakuraAiIntegrations.Remove(sakuraAiGuildIntegration);
                break;
            }
            case CaiGuildIntegration caiGuildIntegration:
            {
                DB.CaiIntegrations.Remove(caiGuildIntegration);
                break;
            }
            case OpenRouterGuildIntegration openRouterGuildIntegration:
            {
                DB.OpenRouterIntegrations.Remove(openRouterGuildIntegration);
                break;
            }
        }

        await DB.SaveChangesAsync();
    }
}
