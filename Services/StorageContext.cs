using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Database;
using Microsoft.EntityFrameworkCore;
using static CharacterEngineDiscord.Services.CommonService;

namespace CharacterEngineDiscord.Services
{
    internal class StorageContext : DbContext
    {
        internal DbSet<Channel> Channels { get; set; }
        internal DbSet<Guild> Guilds { get; set; }
        internal DbSet<IgnoredUser> IgnoredUsers { get; set; }
        internal DbSet<Character> Characters { get; set; }
        internal DbSet<CharacterWebhook> CharacterWebhooks { get; set; }
        internal DbSet<History> Histories { get; set; }
        internal DbSet<HuntedUser> HuntedUsers { get; set; }

        public StorageContext()
            => Database.Migrate();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connString = string.IsNullOrWhiteSpace(ConfigFile.DbConnString.Value) ?
                   $"Data Source={EXE_DIR}storage{sc}{ConfigFile.DbFileName.Value}" :
                   ConfigFile.DbConnString.Value;

            Log("Database connection: "); LogYellow(connString + "\n\n");
            optionsBuilder.UseLazyLoadingProxies().UseSqlite(connString);
        }

        protected internal static async Task<Guild> FindOrStartTrackingGuildAsync(ulong guildId, StorageContext db)
        {
            var guild = await db.Guilds.FindAsync(guildId);

            if (guild is null)
            {
                guild = new() { Id = guildId };
                db.Guilds.Add(guild);
                await db.SaveChangesAsync();
            }

            return guild;
        }

        protected internal static async Task<Channel> FindOrStartTrackingChannelAsync(ulong channelId, ulong guildId, StorageContext db)
        {
            var channel = await db.Channels.FindAsync(channelId);

            if (channel is null)
            {
                channel = new() { Id = channelId, Guild = await FindOrStartTrackingGuildAsync(guildId, db) };
                db.Channels.Add(channel);
                await db.SaveChangesAsync();
            }

            return channel;
        }

        protected internal static async Task<Character> FindOrStartTrackingCharacterAsync(Character character, StorageContext db)
        {
            var record = await db.Characters.FindAsync(character.Id);

            if (record is null)
            {
                db.Characters.Add(character);
                await db.SaveChangesAsync();
            }

            return character;
        }
    }
}
