using CharacterEngineDiscord.Models.Db;
using CharacterEngineDiscord.Models.Db.Discord;
using CharacterEngineDiscord.Models.Db.Integrations;
using CharacterEngineDiscord.Models.Db.SpawnedCharacters;
using Microsoft.EntityFrameworkCore;

// ReSharper disable once CheckNamespace
namespace CharacterEngineDiscord.Models;


public sealed class AppDbContext : DbContext
{
    private readonly string CONNECTION_STRING;

    // public DbSet<DiscordChannelSpawnedCharacter> DiscordChannelSpawnedCharacters { get; init; }
    public DbSet<SakuraAiSpawnedCharacter> SakuraAiSpawnedCharacters { get; init; }

    // public DbSet<DiscordGuildIntegration> DiscordGuildIntegrations { get; init; }
    public DbSet<SakuraAiIntegration> SakuraAiIntegrations { get; init; }


    public DbSet<Manager> Managers { get; init; }
    public DbSet<StoredAction> StoredActions { get; init; }

    // Discord
    public DbSet<DiscordChannel> DiscordChannels { get; init; }
    public DbSet<DiscordGuild> DiscordGuilds { get; init; }


    public AppDbContext(string connectionString)
    {
        CONNECTION_STRING = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(CONNECTION_STRING);
    }
}
