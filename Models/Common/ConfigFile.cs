using Newtonsoft.Json.Linq;
using CharacterEngineDiscord.Services;

namespace CharacterEngineDiscord.Models.Common
{
    public static class ConfigFile
    {
        public static ConfigField DbFileName { get; } = new("db_file_name");
        public static ConfigField DbConnString { get; } = new("db_connection_string");
        public static ConfigField DbLogEnabled { get; } = new("db_enable_logging");
        public static ConfigField NoPermissionFile { get; } = new("no_permission_file");

        public static ConfigField DiscordBotToken { get; } = new("discord_bot_token");
        public static ConfigField DiscordBotRole { get; } = new("discord_bot_manager_role");

        public static ConfigField HosterDiscordID { get; } = new("hoster_discord_id");
        public static ConfigField DiscordLogsChannelID { get; } = new("discord_logs_channel_id");
        public static ConfigField RateLimit { get; } = new("rate_limit");
        public static ConfigField MaxCharactersPerChannel { get; } = new("max_characters_per_channel");

        public static ConfigField CaiEnabled { get; } = new("use_character_ai");
        public static ConfigField DefaultCaiUserAuthToken { get; } = new("character_ai_default_user_auth_token");
        public static ConfigField DefaultCaiPlusMode { get; } = new("character_ai_default_plus_mode");
        public static ConfigField PuppeteerBrowserType { get; } = new("puppeteer_browser_type");
        public static ConfigField PuppeteerBrowserDir { get; } = new("puppeteer_browser_directory");
        public static ConfigField PuppeteerBrowserExe { get; } = new("puppeteer_browser_executable_path");

        public static ConfigField DefaultOpenAiApiEndpoint { get; } = new("open_ai_default_api_endpoint");
        public static ConfigField DefaultOpenAiApiToken { get; } = new("open_ai_default_api_token");
        public static ConfigField DefaultOpenAiModel { get; } = new("open_ai_default_model");

        private static JObject ConfigParsed { get; } = CommonService.TryToParseConfigFile();

        public class ConfigField
        {
            public readonly string Label;
            public string? Value {
                get
                {
                    string? data = ConfigParsed[Label]?.Value<string?>();
                    return string.IsNullOrWhiteSpace(data) ? null : data;
                }
            }

            public ConfigField(string label)
            {
                Label = label;
            }
        }
    }
}
