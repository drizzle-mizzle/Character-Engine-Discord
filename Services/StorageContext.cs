using CharacterEngineDiscord.Models.Database;
using Microsoft.EntityFrameworkCore;
using static CharacterEngineDiscord.Services.CommonService;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngineDiscord.Services
{
    internal class StorageContext : DbContext
    {
        internal DbSet<Models.Database.BlockedGuild> BlockedGuilds { get; set; }
        internal DbSet<Models.Database.BlockedUser> BlockedUsers { get; set; }
        internal DbSet<Models.Database.Channel> Channels { get; set; }
        internal DbSet<Models.Database.Character> Characters { get; set; }
        internal DbSet<Models.Database.CharacterWebhook> CharacterWebhooks { get; set; }
        internal DbSet<Models.Database.Guild> Guilds { get; set; }
        internal DbSet<Models.Database.HuntedUser> HuntedUsers { get; set; }
        internal DbSet<Models.Database.StoredHistoryMessage> StoredHistoryMessages { get; set; }

#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
        public StorageContext()
        {

        }
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connString = ConfigFile.DbConnString.Value.IsEmpty() ?
                   $"Data Source={EXE_DIR}{SC}storage{SC}{ConfigFile.DbFileName.Value}" :
                   ConfigFile.DbConnString.Value;

            optionsBuilder.UseSqlite(connString).UseLazyLoadingProxies(true);

            if (Environment.GetEnvironmentVariable("READY") is not null)
            { 
                if (ConfigFile.DbLogEnabled.Value.ToBool())
                    optionsBuilder.LogTo(SqlLog, new[] { DbLoggerCategory.Database.Command.Name })
                                  .EnableSensitiveDataLogging(true)
                                  .EnableDetailedErrors(true);
            }
        }


        protected internal static void SqlLog(string text)
        {
            int ww;
            try
            {
                ww = Console.WindowWidth;
            }
            catch
            {
                ww = 320;
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(new string('~', ww));

            if (text.Contains("INSERT"))
                Console.ForegroundColor = ConsoleColor.Green;
            else if (text.Contains("DELETE"))
                Console.ForegroundColor = ConsoleColor.Magenta;
            else if (text.Contains("UPDATE"))
                Console.ForegroundColor = ConsoleColor.Yellow;
            else if (text.Contains("SELECT"))
                Console.ForegroundColor = ConsoleColor.Gray;

            Console.WriteLine(text);
            Console.ResetColor();
        }

        protected internal static async Task TryToSaveDbChangesAsync(StorageContext db)
        {
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException e)
            {
                foreach (var entry in e.Entries)
                {
                    entry.Reload();
                }

                try
                {
                    db.SaveChanges();
                }
                catch
                {
                    return;
                }
            }
        }

        protected internal static async Task<Guild> FindOrStartTrackingGuildAsync(ulong guildId, StorageContext? db = null)
        {
            db ??= new StorageContext();
            var guild = await db.Guilds.FindAsync(guildId);

            if (guild is null)
            {
                guild = new() { Id = guildId, MessagesSent = 0 };
                await db.Guilds.AddAsync(guild);
                await TryToSaveDbChangesAsync(db);
                return await FindOrStartTrackingGuildAsync(guildId, db);
            }

            return guild;
        }

        protected internal static async Task<Channel> FindOrStartTrackingChannelAsync(ulong channelId, ulong guildId, StorageContext? db = null)
        {
            db ??= new StorageContext();
            var channel = await db.Channels.FindAsync(channelId);

            if (channel is null)
            {
                channel = new() { Id = channelId, GuildId = (await FindOrStartTrackingGuildAsync(guildId, db)).Id, RandomReplyChance = 0 };
                await db.Channels.AddAsync(channel);
                await TryToSaveDbChangesAsync(db);
                return await FindOrStartTrackingChannelAsync(channelId, guildId, db);
            }

            return channel;
        }

        protected internal static async Task<Models.Database.Character> FindOrStartTrackingCharacterAsync(Models.Database.Character notSavedCharacter, StorageContext? db = null)
        {
            db ??= new StorageContext();
            var character = await db.Characters.FindAsync(notSavedCharacter.Id);

            if (character is null)
            {
                character = (await db.Characters.AddAsync(notSavedCharacter)).Entity;
                await TryToSaveDbChangesAsync(db);
            }
            else
            {   // Update dynamic info
                character.AuthorName = notSavedCharacter.AuthorName;
                character.Name = notSavedCharacter.Name;
                character.Stars = notSavedCharacter.Stars;
                character.Interactions = notSavedCharacter.Interactions;
                character.Title = notSavedCharacter.Title;
                character.Greeting = notSavedCharacter.Greeting;
                character.Description = notSavedCharacter.Description;
                character.Definition = notSavedCharacter.Definition;
                character.AvatarUrl = notSavedCharacter.AvatarUrl;
            }

            return character;
        }
    }
}
