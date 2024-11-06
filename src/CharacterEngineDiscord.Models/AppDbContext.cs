using CharacterEngineDiscord.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Models.Db;
using CharacterEngineDiscord.Models.Db.Discord;
using CharacterEngineDiscord.Models.Db.Integrations;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using Microsoft.EntityFrameworkCore;

// ReSharper disable once CheckNamespace
namespace CharacterEngineDiscord.Models;


public sealed class AppDbContext : DbContext
{
    private readonly string CONNECTION_STRING = null!;


    #region Discord

    public DbSet<DiscordChannel> DiscordChannels { get; init; }
    public DbSet<DiscordGuild> DiscordGuilds { get; init; }

    #endregion


    #region Integrations

    public DbSet<SakuraAiGuildIntegration> SakuraAiIntegrations { get; init; }

    public DbSet<CaiGuildIntegration> CaiIntegrations { get; init; }

    #endregion


    #region SpawnedCharacters

    public DbSet<SakuraAiSpawnedCharacter> SakuraAiSpawnedCharacters { get; init; }

    public DbSet<CaiSpawnedCharacter> CaiSpawnedCharacters { get; init; }

    #endregion


    #region Bot

    public DbSet<BlockedGuildUser> BlockedGuildUsers { get; init; }
    public DbSet<BlockedUser> BlockedUsers { get; init; }
    public DbSet<GuildBotManager> GuildBotManagers { get; init; }

    #endregion


    #region Application

    public DbSet<Metric> Metrics { get; init; }
    public DbSet<StoredAction> StoredActions { get; init; }

    #endregion

    public AppDbContext() { }

    public AppDbContext(string connectionString)
    {
        CONNECTION_STRING = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(CONNECTION_STRING);
    }
}
