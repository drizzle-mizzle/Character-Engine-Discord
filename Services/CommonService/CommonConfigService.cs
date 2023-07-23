using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace CharacterEngineDiscord.Services
{
    internal static partial class CommonService
    {
        internal static JObject TryToParseConfigFile()
        {
            try
            {
                using StreamReader sr = new(CONFIG_PATH);
                string content = sr.ReadToEnd();
                var file = (JObject)JsonConvert.DeserializeObject(content)!;

                return file;
            }
            catch (Exception e)
            {
                LogRed("\nSomething went wrong...\nCheck your ");
                LogYellow("config.json ");
                LogRed($"file. (probably, missing some comma or quotation mark?)\n\nDetails:\n{e}");

                throw;
            }
        }

        internal static void SetEnvs()
        {
            Environment.SetEnvironmentVariable("RUNNING", "!");

            string path = $"{EXE_DIR}env.json";
            if (!File.Exists(path)) return;

            try
            {
                using StreamReader sr = new(path);
                var content = sr.ReadToEnd();
                var env = JsonConvert.DeserializeObject(content) as JObject;

                string? envDiscordToken = env?["DISCORD_TOKEN"]?.Value<string?>();
                string? envCAIToken = env?["DEFAULT_CAI_TOKEN"]?.Value<string?>();

                Environment.SetEnvironmentVariable("DISCORD_TOKEN", envDiscordToken, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("DEFAULT_CAI_TOKEN", envCAIToken, EnvironmentVariableTarget.Process);
            }
            catch { }
        }
    }
}
