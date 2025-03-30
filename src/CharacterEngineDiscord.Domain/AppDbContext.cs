using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using CharacterEngineDiscord.Domain.Models.Db.Integrations;
using CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// ReSharper disable once CheckNamespace
namespace CharacterEngineDiscord.Models;


public sealed class AppDbContext : DbContext
{
    private readonly string CONNECTION_STRING = null!;

    public AppDbContext() { }

    public AppDbContext(string connectionString)
    {
        CONNECTION_STRING = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(CONNECTION_STRING, options => options.MigrationsAssembly("CharacterEngineDiscord.Migrator"))
                      .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OpenRouterSpawnedCharacter>(sc =>
        {
            sc.Property(s => s.OpenRouterModel).IsRequired();
            sc.Property(s => s.OpenRouterTemperature).IsRequired();
            sc.Property(s => s.OpenRouterTopP).IsRequired();
            sc.Property(s => s.OpenRouterTopK).IsRequired();
            sc.Property(s => s.OpenRouterFrequencyPenalty).IsRequired();
            sc.Property(s => s.OpenRouterPresencePenalty).IsRequired();
            sc.Property(s => s.OpenRouterRepetitionPenalty).IsRequired();
            sc.Property(s => s.OpenRouterMinP).IsRequired();
            sc.Property(s => s.OpenRouterTopA).IsRequired();
            sc.Property(s => s.OpenRouterMaxTokens).IsRequired();
        });
    }


    #region Discord

    public DbSet<DiscordChannel> DiscordChannels { get; init; }
    public DbSet<DiscordGuild> DiscordGuilds { get; init; }
    public DbSet<DiscordUser> DiscordUsers { get; init; }

    #endregion


    #region Integrations

    public DbSet<SakuraAiGuildIntegration> SakuraAiIntegrations { get; init; }

    public DbSet<CaiGuildIntegration> CaiIntegrations { get; init; }

    public DbSet<OpenRouterGuildIntegration> OpenRouterIntegrations { get; init; }

    #endregion


    #region SpawnedCharacters

    public DbSet<SakuraAiSpawnedCharacter> SakuraAiSpawnedCharacters { get; init; }

    public DbSet<CaiSpawnedCharacter> CaiSpawnedCharacters { get; init; }

    public DbSet<OpenRouterSpawnedCharacter> OpenRouterSpawnedCharacters { get; init; }

    #endregion


    #region Bot

    public DbSet<BlockedGuildUser> GuildBlockedUsers { get; init; }
    public DbSet<BlockedUser> BlockedUsers { get; init; }
    public DbSet<CharacterChatHistory> ChatHistories { get; init; }
    public DbSet<GuildBotManager> GuildBotManagers { get; init; }
    public DbSet<HuntedUser> HuntedUsers { get; init; }

    #endregion


    #region Application

    public DbSet<Metric> Metrics { get; init; }
    public DbSet<StoredAction> StoredActions { get; init; }

    #endregion
}
