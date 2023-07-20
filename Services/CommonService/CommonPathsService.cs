using CharacterEngineDiscord.Models;

namespace CharacterEngineDiscord.Services
{
    internal static partial class CommonService
    {
        internal static string sc = $"{Path.DirectorySeparatorChar}";
        internal static string EXE_DIR = AppDomain.CurrentDomain.BaseDirectory;
        internal static string CONFIG_PATH = $"{EXE_DIR}config.json";
    }
}
