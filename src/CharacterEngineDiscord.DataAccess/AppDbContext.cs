using CharacterEngineDiscord.DataAccess.Models.Discord;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.DataAccess;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<DiscordGuild> DiscordGuilds => Set<DiscordGuild>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
