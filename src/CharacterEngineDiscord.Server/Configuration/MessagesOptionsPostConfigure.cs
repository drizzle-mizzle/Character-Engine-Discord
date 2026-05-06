using CharacterEngineDiscord.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.Server.Configuration;

/// <summary>
/// Reads multi-line text templates referenced by <see cref="MessagesOptions"/> file-name
/// fields and stuffs the contents back into the runtime <c>Default*</c> properties.
/// Files live next to the binary in <c>Settings/</c> (copied to output by the csproj).
/// Missing or unreadable files are logged as warnings and leave the field as <c>null</c>;
/// they are not fatal during bootstrap.
/// </summary>
internal sealed class MessagesOptionsPostConfigure : IPostConfigureOptions<MessagesOptions>
{
    private readonly ILogger<MessagesOptionsPostConfigure> _logger;

    public MessagesOptionsPostConfigure(ILogger<MessagesOptionsPostConfigure> logger)
    {
        _logger = logger;
    }

    public void PostConfigure(string? name, MessagesOptions options)
    {
        var settingsDir = Path.Combine(AppContext.BaseDirectory, "Settings");

        options.DefaultMessagesFormat = LoadFileOrNull(settingsDir, options.DefaultMessagesFormatFile, nameof(MessagesOptions.DefaultMessagesFormatFile));
        options.DefaultSystemPrompt = LoadFileOrNull(settingsDir, options.DefaultSystemPromptFile, nameof(MessagesOptions.DefaultSystemPromptFile));
    }

    private string? LoadFileOrNull(string dir, string filename, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return null;
        }

        var fullPath = Path.Combine(dir, filename);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("{Field} '{Path}' not found; default value will be null", fieldName, fullPath);
            return null;
        }

        try
        {
            return File.ReadAllText(fullPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "{Field} '{Path}' could not be read; default value will be null", fieldName, fullPath);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "{Field} '{Path}' could not be read; default value will be null", fieldName, fullPath);
            return null;
        }
    }
}
