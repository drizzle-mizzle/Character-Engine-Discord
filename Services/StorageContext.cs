using CharacterEngineDiscord.Models.Database;
using Microsoft.EntityFrameworkCore;
using static CharacterEngineDiscord.Services.CommonService;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngineDiscord.Services
{
    internal class StorageContext : DbContext
    {
        internal DbSet<BlockedGuild> BlockedGuilds { get; set; }
        internal DbSet<BlockedUser> BlockedUsers { get; set; }
        internal DbSet<CaiHistory> CaiHistories { get; set; }
        internal DbSet<Channel> Channels { get; set; }
        internal DbSet<Character> Characters { get; set; }
        internal DbSet<CharacterWebhook> CharacterWebhooks { get; set; }
        internal DbSet<Guild> Guilds { get; set; }
        internal DbSet<HuntedUser> HuntedUsers { get; set; }
        internal DbSet<OpenAiHistoryMessage> OpenAiHistoryMessages { get; set; }

        public StorageContext()
        {
            if (Environment.GetEnvironmentVariable("RUNNING") is not null) // Needed for migration builds, these have no Console
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(new string('~', Console.WindowWidth));
                Console.ResetColor();
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connString = ConfigFile.DbConnString.Value.IsEmpty() ?
                   $"Data Source={EXE_DIR}{SC}storage{SC}{ConfigFile.DbFileName.Value}" :
                   ConfigFile.DbConnString.Value;

            optionsBuilder.UseSqlite(connString).UseLazyLoadingProxies(true);

            if (ConfigFile.DbLogEnabled.Value.ToBool())
                optionsBuilder.LogTo(SqlLog, new[] { DbLoggerCategory.Database.Command.Name });
        }


        protected internal static void SqlLog(string text)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(new string('~', Console.WindowWidth - 1) + "\n");
            Console.ResetColor();
            LogYellow(text + "\n");
        }

        protected internal static async Task<Guild> FindOrStartTrackingGuildAsync(ulong guildId, StorageContext db)
        {
            var guild = await db.Guilds.FindAsync(guildId);

            if (guild is null)
            {
                guild = new() { Id = guildId, BtnsRemoveDelay = 90, GuildCaiPlusMode = false, GuildCaiUserToken = null, GuildOpenAiApiToken = null, GuildOpenAiModel = null, GuildOpenAiApiEndpoint = null, GuildMessagesFormat = "{{msg}}" };
                await db.Guilds.AddAsync(guild);
                await db.SaveChangesAsync();
            }

            await db.Entry(guild).ReloadAsync();
            return guild;
        }

        protected internal static async Task<Channel> FindOrStartTrackingChannelAsync(ulong channelId, ulong guildId, StorageContext db)
        {
            var channel = await db.Channels.FindAsync(channelId);

            if (channel is null)
            {
                channel = new() { Id = channelId, GuildId = (await FindOrStartTrackingGuildAsync(guildId, db)).Id, RandomReplyChance = 0 };
                await db.Channels.AddAsync(channel);
                await db.SaveChangesAsync();
            }

            await db.Entry(channel).ReloadAsync();
            return channel;
        }

        protected internal static async Task<Character> FindOrStartTrackingCharacterAsync(Character notSavedCharacter, StorageContext db)
        {
            var character = await db.Characters.FindAsync(notSavedCharacter.Id);

            if (character is null)
            {
                character = db.Characters.Add(notSavedCharacter).Entity;
                await db.SaveChangesAsync();
            }

            await db.Entry(character).ReloadAsync();
            return character;
        }
    }
}
