using CharacterEngineDiscord.Models.Common;

namespace CharacterEngineDiscord.Services
{
    internal static partial class CommonService
    {
        internal static void Log(object? text)
        {
            Console.Write($"{text + (text is string ? "" : "\n")}");
        }

        internal static void LogGreen(object? text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Log(text);
            Console.ResetColor();
        }
        internal static void LogRed(object? text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Log(text);
            Console.ResetColor();
        }

        internal static void LogYellow(object? text)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Log(text);
            Console.ResetColor();
        }

        internal static void LogException(Exception e)
            => LogException(e.ToString());

        internal static void LogException(string title, Exception e)
            => LogException($"| {title}\n{e}");

        internal static void LogException(string text)
        {
            LogRed(new string('~', Console.WindowWidth - 1) + "\n");
            LogRed($"[{DateTime.Now:u}] {text}\n");
            LogRed(new string('~', Console.WindowWidth - 1) + "\n");

            if (!ConfigFile.LogFileEnabled.Value.ToBool()) return;

            try { File.AppendAllText($"{EXE_DIR}{SC}logs.txt", $"{new string('~', 10)}\n[{DateTime.Now:u}] {text}\n"); }
            catch (Exception e) { LogRed(e); }
        }
    }
}
