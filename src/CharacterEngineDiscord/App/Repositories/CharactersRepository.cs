using System.Text.RegularExpressions;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Repositories.Abstractions;
using CharacterEngine.App.Services;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Abstractions.Adapters;
using CharacterEngineDiscord.Shared.Abstractions.Characters;
using CharacterEngineDiscord.Shared.Abstractions.Sources.OpenRouter;
using CharacterEngineDiscord.Shared.Models;
using Discord;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace CharacterEngine.App.Repositories;


public class CharactersRepository : RepositoryBase
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly SemaphoreSlim _deletionLock = new(1, 1);


    public CharactersRepository(AppDbContext db) : base(db) { }


    public async Task<List<ISpawnedCharacter>> GetAllSpawnedCharactersAsync()
    {
        var result = new List<ISpawnedCharacter>();

        result.AddRange(await DB.SakuraAiSpawnedCharacters.ToListAsync());
        result.AddRange(await DB.CaiSpawnedCharacters.ToListAsync());
        result.AddRange(await DB.OpenRouterSpawnedCharacters.ToListAsync());

        return result;
    }


    public async Task<List<ISpawnedCharacter>> GetAllSpawnedCharactersInChannelAsync(ulong channelId)
    {
        var result = new List<ISpawnedCharacter>();

        result.AddRange(await DB.SakuraAiSpawnedCharacters.Where(c => c.DiscordChannelId == channelId).ToListAsync());
        result.AddRange(await DB.CaiSpawnedCharacters.Where(c => c.DiscordChannelId == channelId).ToListAsync());
        result.AddRange(await DB.OpenRouterSpawnedCharacters.Where(c => c.DiscordChannelId == channelId).ToListAsync());

        return result;
    }


    public async Task<List<ISpawnedCharacter>> GetAllSpawnedCharactersInGuildAsync(ulong guildId)
    {
        var result = new List<ISpawnedCharacter>();

        result.AddRange(await DB.SakuraAiSpawnedCharacters.Include(c => c.DiscordChannel).Where(c => c.DiscordChannel.DiscordGuildId == guildId).ToListAsync());
        result.AddRange(await DB.CaiSpawnedCharacters.Include(c => c.DiscordChannel).Where(c => c.DiscordChannel.DiscordGuildId == guildId).ToListAsync());
        result.AddRange(await DB.OpenRouterSpawnedCharacters.Include(c => c.DiscordChannel).Where(c => c.DiscordChannel.DiscordGuildId == guildId).ToListAsync());

        return result;
    }


    public async Task<ISpawnedCharacter?> GetSpawnedCharacterByIdAsync(Guid spawnedCharacterId)
    {
        return await DB.SakuraAiSpawnedCharacters.FindAsync(spawnedCharacterId) as ISpawnedCharacter
            ?? await DB.CaiSpawnedCharacters.FindAsync(spawnedCharacterId) as ISpawnedCharacter
            ?? await DB.OpenRouterSpawnedCharacters.FindAsync(spawnedCharacterId) as ISpawnedCharacter;
    }


    public async Task UpdateSpawnedCharacterAsync(ISpawnedCharacter spawnedCharacter)
    {
        switch (spawnedCharacter)
        {
            case SakuraAiSpawnedCharacter sakuraAiSpawnedCharacter:
            {
                DB.SakuraAiSpawnedCharacters.Update(sakuraAiSpawnedCharacter);
                break;
            }
            case CaiSpawnedCharacter caiSpawnedCharacter:
            {
                DB.CaiSpawnedCharacters.Update(caiSpawnedCharacter);
                break;
            }
            case OpenRouterSpawnedCharacter openRouterSpawnedCharacter:
            {
                DB.OpenRouterSpawnedCharacters.Update(openRouterSpawnedCharacter);
                break;
            }
        }

        await DB.SaveChangesAsync();
    }


    public async Task DeleteSpawnedCharacterAsync(Guid spawnedCharacterId)
    {
        await _deletionLock.WaitAsync();

        try
        {
            var spawnedCharacter = await GetSpawnedCharacterByIdAsync(spawnedCharacterId);

            if (spawnedCharacter is null)
            {
                return;
            }

            await DeleteSpawnedCharacterAsync(spawnedCharacter);
            await DB.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException e)
        {
            _logger.Warn(e.ToString);
        }
        finally
        {
            _deletionLock.Release();
        }
    }


    public async Task DeleteSpawnedCharactersAsync(IReadOnlyCollection<ISpawnedCharacter> spawnedCharacters)
    {
        await _deletionLock.WaitAsync();

        try
        {
            foreach (var spawnedCharacter in spawnedCharacters)
            {
                await DeleteSpawnedCharacterAsync(spawnedCharacter);
            }

            await DB.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException e)
        {
            _logger.Warn(e.ToString);
        }
        finally
        {
            _deletionLock.Release();
        }
    }

    private async Task DeleteSpawnedCharacterAsync(ISpawnedCharacter spawnedCharacter)
    {
        if (spawnedCharacter is IAdoptedCharacter)
        {
            var history = await DB.ChatHistories.Where(message => message.SpawnedCharacterId == spawnedCharacter.Id).ToArrayAsync();

            if (history.Length > 0)
            {
                DB.ChatHistories.RemoveRange(history);
            }
        }

        switch (spawnedCharacter)
        {
            case SakuraAiSpawnedCharacter sakuraAiSpawnedCharacter:
            {
                DB.SakuraAiSpawnedCharacters.Remove(sakuraAiSpawnedCharacter);
                break;
            }
            case CaiSpawnedCharacter caiSpawnedCharacter:
            {
                DB.CaiSpawnedCharacters.Remove(caiSpawnedCharacter);
                break;
            }
            case OpenRouterSpawnedCharacter openRouterSpawnedCharacter:
            {
                DB.OpenRouterSpawnedCharacters.Remove(openRouterSpawnedCharacter);
                break;
            }
        }

    }


    private static readonly Regex FILTER_REGEX = new(@"[^a-zA-Z0-9\s]", RegexOptions.Compiled);
    public async Task<ISpawnedCharacter> CreateSpawnedCharacterAsync(CommonCharacter commonCharacter, IWebhook webhook, IGuildIntegration guildIntegration)
    {
        var characterName = FILTER_REGEX.Replace(commonCharacter.CharacterName.Trim(), string.Empty);
        if (characterName.Length == 0)
        {
            throw new UserFriendlyException("Invalid character name");
        }

        string callPrefix;
        if (characterName.Contains(' '))
        {
            var split = characterName.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            callPrefix = $"@{split[0][0]}{split[1][0]}";
        }
        else if (characterName.Length > 2)
        {
            callPrefix = $"@{characterName[..2]}";
        }
        else
        {
            callPrefix = $"@{characterName}";
        }

        var searchModule = commonCharacter.CharacterSourceType is CharacterSourceType sourceType
            ? IntegrationsHub.GetSearchModule(sourceType)
            : IntegrationsHub.GetSearchModule(commonCharacter.IntegrationType);

        var characterAdapter = await searchModule.GetCharacterInfoAsync(commonCharacter.CharacterId, guildIntegration);
        var fullCommonCharacter = characterAdapter.ToCommonCharacter();

        ISpawnedCharacter newSpawnedCharacter;

        switch (commonCharacter.IntegrationType)
        {
            case IntegrationType.SakuraAI:
            {
                var sakuraAiSpawnedCharacter = new SakuraAiSpawnedCharacter((IAdoptableCharacterAdapter)characterAdapter);
                newSpawnedCharacter = DB.SakuraAiSpawnedCharacters.Add(sakuraAiSpawnedCharacter).Entity;
                break;
            }
            case IntegrationType.CharacterAI:
            {
                var caiSpawnedCharacter = new CaiSpawnedCharacter(characterAdapter);
                newSpawnedCharacter = DB.CaiSpawnedCharacters.Add(caiSpawnedCharacter).Entity;
                break;
            }
            case IntegrationType.OpenRouter:
            {
                var openRouterSpawnedCharacter = new OpenRouterSpawnedCharacter((IAdoptableCharacterAdapter)characterAdapter, (IOpenRouterIntegration)guildIntegration);
                newSpawnedCharacter = DB.OpenRouterSpawnedCharacters.Add(openRouterSpawnedCharacter).Entity;
                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException(nameof(commonCharacter.IntegrationType));
            }
        }

        newSpawnedCharacter.CharacterId = fullCommonCharacter.CharacterId;
        newSpawnedCharacter.CharacterName = fullCommonCharacter.CharacterName;
        newSpawnedCharacter.CharacterFirstMessage = fullCommonCharacter.CharacterFirstMessage ?? ":wave:";
        newSpawnedCharacter.CharacterImageLink = fullCommonCharacter.CharacterImageLink;
        newSpawnedCharacter.CharacterAuthor = fullCommonCharacter.CharacterAuthor ?? "unknown";
        newSpawnedCharacter.IsNfsw = fullCommonCharacter.IsNfsw;
        newSpawnedCharacter.DiscordChannelId = (ulong)webhook.ChannelId!;
        newSpawnedCharacter.WebhookId = webhook.Id;
        newSpawnedCharacter.WebhookToken = webhook.Token;
        newSpawnedCharacter.CallPrefix = callPrefix.ToLower();
        newSpawnedCharacter.ResponseDelay = 3;
        newSpawnedCharacter.FreewillFactor = 3;
        newSpawnedCharacter.EnableSwipes = false;
        newSpawnedCharacter.FreewillContextSize = 3000;
        newSpawnedCharacter.EnableQuotes = false;
        newSpawnedCharacter.EnableStopButton = true;
        newSpawnedCharacter.SkipNextBotMessage = false;
        newSpawnedCharacter.LastCallerDiscordUserId = 0;
        newSpawnedCharacter.LastDiscordMessageId = 0;
        newSpawnedCharacter.MessagesSent = 0;
        newSpawnedCharacter.LastCallTime = DateTime.Now;

        await DB.SaveChangesAsync();

        return newSpawnedCharacter;
    }

}
