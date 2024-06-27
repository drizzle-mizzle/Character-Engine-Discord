using CharacterEngine.Helpers;
using CharacterEngine.Models.Db;
using CharacterEngine.Models.Db.Discord;
using CharacterEngine.Models.Db.Integrations;
using CharacterEngine.Models.Db.SpawnedCharacters;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.Database;

public sealed class AppDbContext : DbContext
{
    public DbSet<SakuraAiGuildIntegration> SakuraFmGuildIntegrations { get; set; }
    public DbSet<SakuraAiSpawnedCharacter> SakuraFmSpawnedCharacters { get; set; }
    public DbSet<DiscordGuildIntegration> DiscordGuildIntegrations { get; set; }
    public DbSet<DiscordChannelSpawnedCharacter> DiscordChannelSpawnedCharacters { get; set; }
    public DbSet<StoredAction> StoredActions { get; set; }

    // Discord
    public DbSet<DiscordChannel> DiscordChannels { get; set; }
    public DbSet<DiscordGuild> DiscordGuilds { get; set; }


    public AppDbContext()
    {
        Database.EnsureCreated();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(BotConfig.DATABASE_CONNECTION_STRING);
    }
}
