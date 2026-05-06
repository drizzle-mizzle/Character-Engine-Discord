using CharacterEngineDiscord.DataAccess.Models.Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterEngineDiscord.DataAccess.Configurations.Discord;

internal sealed class DiscordGuildConfiguration : IEntityTypeConfiguration<DiscordGuild>
{
    public void Configure(EntityTypeBuilder<DiscordGuild> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
               .ValueGeneratedNever();

        builder.Property(g => g.Name)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(g => g.OwnerUsername)
               .HasMaxLength(100);

        builder.Property(g => g.IconUrl)
               .HasMaxLength(512);

        builder.Property(g => g.Joined)
               .IsRequired();

        builder.Property(g => g.CreatedAt)
               .HasDefaultValueSql("now()")
               .ValueGeneratedOnAdd();

        builder.Property(g => g.UpdatedAt)
               .HasDefaultValueSql("now()")
               .ValueGeneratedOnAdd();

        // Soft-delete marker: regular queries see only currently-joined guilds.
        // Resurrect / audit flows must call IgnoreQueryFilters().
        builder.HasQueryFilter(g => g.Joined);
    }
}
