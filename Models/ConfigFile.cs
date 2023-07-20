using Newtonsoft.Json.Linq;
using CharacterEngineDiscord.Services;

namespace CharacterEngineDiscord.Models
{
    public static class ConfigFile
    {
        public static ConfigField DbFileName { get; } = new("db_file_name");
        public static ConfigField DbConnString { get; } = new("db_connection_string");
        public static ConfigField NoPermissionFile { get; } = new("no_permission_file");

        public static ConfigField DiscordBotToken { get; } = new("discord_bot_token");
        public static ConfigField DiscordBotRole { get; } = new("discord_bot_manager_role");

        public static ConfigField HosterDiscordID { get; } = new("hoster_discord_id");
        public static ConfigField DiscordLogsChannelID { get; } = new("discord_logs_channel_id");
        public static ConfigField RateLimit { get; } = new("rate_limit");

        public static ConfigField UseCAI { get; } = new("use_character_ai");
        public static ConfigField UseCAIplusMode { get; } = new("use_character_ai_plus_mode");
        public static ConfigField CAIuserAuthToken { get; } = new("character_ai_user_auth_token");
        public static ConfigField PuppeteerBrowserType { get; } = new("puppeteer_browser_type");
        public static ConfigField PuppeteerBrowserDir { get; } = new("puppeteer_browser_directory");
        public static ConfigField PuppeteerBrowserExe { get; } = new("puppeteer_browser_executable_path");

        private static JObject ConfigParsed { get; } = CommonService.TryToParseConfigFile();

        public class ConfigField
        {
            public readonly string Label;
            public string? Value { get => ConfigParsed[Label]?.Value<dynamic?>()?.ToString(); }
            
            public ConfigField(string label)
            {
                Label = label;
            }
        }
    }
}
