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

        public static Task TryToSaveDbChangesAsync(StorageContext db) => throw new NotImplementedException();

        public static Task<Guild> FindOrStartTrackingGuildAsync(ulong guildId, StorageContext? db = null) => throw new NotImplementedException();

        public static Task<Channel> FindOrStartTrackingChannelAsync(ulong channelId, ulong guildId, StorageContext? db = null) => throw new NotImplementedException();

        public static Task<Models.Database.Character> FindOrStartTrackingCharacterAsync(Models.Database.Character notSavedCharacter, StorageContext? db = null) => throw new NotImplementedException();
    }
}
