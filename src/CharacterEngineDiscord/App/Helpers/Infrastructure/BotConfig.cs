using NLog;

namespace CharacterEngine.App.Helpers.Infrastructure;

public static class BotConfig
{
    public static string BOT_TOKEN
        => GetParamByName<string>("BOT_TOKEN").Trim();

    public static string PLAYING_STATUS
        => GetParamByName<string>("PLAYING_STATUS").Trim();

    public static ulong[] OWNER_USERS_IDS
        => GetParamByName<string>("OWNER_USERS_IDS").Split(',').Select(ulong.Parse).ToArray();

    public static ulong ADMIN_GUILD_ID
        => GetParamByName<ulong>("ADMIN_GUILD_ID");

    public static string ADMIN_GUILD_INVITE_LINK
        => GetParamByName<string>("ADMIN_GUILD_INVITE_LINK").Trim();

    public static ulong LOGS_CHANNEL_ID
        => GetParamByName<ulong>("LOGS_CHANNEL_ID");

    public static ulong ERRORS_CHANNEL_ID
        => GetParamByName<ulong>("ERRORS_CHANNEL_ID");

    public static string DEFAULT_MESSAGES_FORMAT
        => GetParamByName<string>("DEFAULT_MESSAGES_FORMAT");

    public static int USER_RATE_LIMIT
        => GetParamByName<int>("USER_RATE_LIMIT");

    public static int USER_FIRST_BLOCK_MINUTES
        => GetParamByName<int>("USER_FIRST_BLOCK_MINUTES");

    public static int USER_SECOND_BLOCK_HOURS
        => GetParamByName<int>("USER_SECOND_BLOCK_HOURS");

    public static string NO_POWER_FILE
        => GetParamByName<string>("NO_POWER_FILE");

    public static string DEFAULT_AVATAR_FILE
        => GetParamByName<string>("DEFAULT_AVATAR_FILE");

    public static string DATABASE_CONNECTION_STRING
        => GetParamByName<string>("DATABASE_CONNECTION_STRING");

    public static string SAKURA_AI_EMOJI
        => GetParamByName<string>("SAKURA_AI_EMOJI");

    public static string CHARACTER_AI_EMOJI
        => GetParamByName<string>("CHARACTER_AI_EMOJI");



    private static string CONFIG_PATH = default!;
    public static void Initialize()
    {
        var files = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings"));
        CONFIG_PATH = GetFileThatStartsWith("env.config") ?? GetFileThatStartsWith("config")!;

        LogManager.GetCurrentClassLogger().Info($"[ Config path: {CONFIG_PATH} ]");
        return;

        string? GetFileThatStartsWith(string pattern)
            => files.FirstOrDefault(file => file.Split(Path.DirectorySeparatorChar).Last().StartsWith(pattern));
    }


    private static T GetParamByName<T>(string paramName) where T : notnull
    {
        var configLines = File.ReadAllLines(CONFIG_PATH);
        var neededLine = configLines.First(line => line.Trim().StartsWith(paramName)) + " ";
        var valueIndex = neededLine.IndexOf(':') + 1;
        var configValue = neededLine[valueIndex..].Trim();

        return string.IsNullOrWhiteSpace(configValue) ? default! : (T)Convert.ChangeType(configValue, typeof(T));
    }
}
