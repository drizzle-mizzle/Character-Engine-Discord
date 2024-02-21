using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Database;
using CharacterEngineDiscord.Services;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Interfaces
{
    public interface IStorageContext
    {
        public DbSet<Models.Database.BlockedGuild> BlockedGuilds { get; set; }
        public DbSet<Models.Database.BlockedUser> BlockedUsers { get; set; }
        public DbSet<Models.Database.Channel> Channels { get; set; }
        public DbSet<Models.Database.Character> Characters { get; set; }
        public DbSet<Models.Database.CharacterWebhook> CharacterWebhooks { get; set; }
        public DbSet<Models.Database.Guild> Guilds { get; set; }
        public DbSet<Models.Database.HuntedUser> HuntedUsers { get; set; }
        public DbSet<Models.Database.StoredHistoryMessage> StoredHistoryMessages {  get; set; }

        public static Task TryToSaveDbChangesAsync(DatabaseContext db) => throw new NotImplementedException();

        public static Task<Guild> FindOrStartTrackingGuildAsync(ulong guildId, DatabaseContext? db = null) => throw new NotImplementedException();

        public static Task<Channel> FindOrStartTrackingChannelAsync(ulong channelId, ulong guildId, DatabaseContext? db = null) => throw new NotImplementedException();

        public static Task<Models.Database.Character> FindOrStartTrackingCharacterAsync(Models.Database.Character notSavedCharacter, DatabaseContext? db = null) => throw new NotImplementedException();
    }
}
