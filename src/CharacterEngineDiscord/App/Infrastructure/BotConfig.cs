namespace CharacterEngine.App.Helpers.Infrastructure;

public static class BotConfig
{
    public static readonly string CONFIG_PATH = Initialize();

    public static readonly string BOT_TOKEN = GetParamByName<string>("BOT_TOKEN").Trim();

    public static readonly string PLAYING_STATUS = GetParamByName<string>("PLAYING_STATUS").Trim();

    public static readonly ulong[] OWNER_USERS_IDS = GetParamByName<string>("OWNER_USERS_IDS").Split(',').Select(ulong.Parse).ToArray();

    public static readonly ulong ADMIN_GUILD_ID = GetParamByName<ulong>("ADMIN_GUILD_ID");

    public static readonly string ADMIN_GUILD_INVITE_LINK = GetParamByName<string>("ADMIN_GUILD_INVITE_LINK").Trim();

    public static readonly ulong LOGS_CHANNEL_ID = GetParamByName<ulong>("LOGS_CHANNEL_ID");

    public static readonly ulong ERRORS_CHANNEL_ID = GetParamByName<ulong>("ERRORS_CHANNEL_ID");

    public static readonly string DEFAULT_MESSAGES_FORMAT = GetParamByName<string>("DEFAULT_MESSAGES_FORMAT");

    public static readonly string DEFAULT_SYSTEM_PROMPT = GetParamByName<string>("DEFAULT_SYSTEM_PROMPT");

    public static readonly int USER_RATE_LIMIT = GetParamByName<int>("USER_RATE_LIMIT");

    public static int USER_FIRST_BLOCK_MINUTES
        => GetParamByName<int>("USER_FIRST_BLOCK_MINUTES");

    public static int USER_SECOND_BLOCK_HOURS
        => GetParamByName<int>("USER_SECOND_BLOCK_HOURS");

    public static string DEFAULT_AVATAR_FILE
        => GetParamByName<string>("DEFAULT_AVATAR_FILE");


    public static readonly string DATABASE_CONNECTION_STRING = GetParamByName<string>("DATABASE_CONNECTION_STRING");

    public static readonly string SAKURA_AI_EMOJI = GetParamByName<string>("SAKURA_AI_EMOJI");

    public static readonly string CHARACTER_AI_EMOJI = GetParamByName<string>("CHARACTER_AI_EMOJI");

    public static readonly string OPEN_ROUTER_EMOJI = GetParamByName<string>("OPEN_ROUTER_EMOJI");

    public static readonly string CHUB_AI_EMOJI = GetParamByName<string>("CHUB_AI_EMOJI");



    private static string Initialize()
    {
        var files = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings"));
        var path = GetFileThatStartsWith("env.config") ?? GetFileThatStartsWith("config")!;

        return path;

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
