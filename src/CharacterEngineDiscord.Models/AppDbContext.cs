using CharacterEngineDiscord.Db.Models.Db;
using CharacterEngineDiscord.Db.Models.Db.Discord;
using CharacterEngineDiscord.Db.Models.Db.Integrations;
using CharacterEngineDiscord.Db.Models.Db.SpawnedCharacters;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Db;

public sealed class AppDbContext : DbContext
{
    private readonly string CONNECTION_STRING;

    public DbSet<SakuraAiGuildIntegration> SakuraFmGuildIntegrations { get; init; }
    public DbSet<SakuraAiSpawnedCharacter> SakuraFmSpawnedCharacters { get; init; }
    public DbSet<DiscordGuildIntegration> DiscordGuildIntegrations { get; init; }
    public DbSet<DiscordChannelSpawnedCharacter> DiscordChannelSpawnedCharacters { get; init; }
    public DbSet<StoredAction> StoredActions { get; init; }

    // Discord
    public DbSet<DiscordChannel> DiscordChannels { get; init; }
    public DbSet<DiscordGuild> DiscordGuilds { get; init; }


    public AppDbContext(string connectionString)
    {
        CONNECTION_STRING = connectionString;

        Database.EnsureCreated();
        Database.Migrate();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(CONNECTION_STRING);
    }
}
