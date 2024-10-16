﻿namespace CharacterEngine.App.Helpers.Infrastructure;

public static class BotConfig
{
    public static string BOT_TOKEN
        => GetParamByName<string>("BOT_TOKEN").Trim();

    public static string PLAYING_STATUS
        => GetParamByName<string>("PLAYING_STATUS").Trim();

    public static ulong[] OWNER_USERS_IDS
        => GetParamByName<string>("OWNER_USERS_IDS").Split(',').Select(ulong.Parse).ToArray();

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


    // Private

    private static string? GetFileThatStartsWith(this string[] paths, string pattern)
        => paths.FirstOrDefault(file => file.Split(Path.DirectorySeparatorChar).Last().StartsWith(pattern));

    private static string? _configPath;
    private static string CONFIG_PATH
    {
        get
        {
            if (_configPath is not null)
            {
                return _configPath;
            }

            var files = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings"));
            return _configPath = files.GetFileThatStartsWith("env.config") ??
                                 files.GetFileThatStartsWith("config")!;
        }
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
