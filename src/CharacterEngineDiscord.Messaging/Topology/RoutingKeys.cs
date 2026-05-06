using System.Text;

namespace CharacterEngineDiscord.Messaging.Topology;

/// <summary>
/// Centralised routing key vocabulary for the Character Engine bus.
/// Every concrete request type publishes to <c>ce.request.{kebab-name}</c>;
/// every concrete command publishes to <c>ce.command.{kebab-name}</c>.
/// </summary>
public static class RoutingKeys
{
    public const string RequestPrefix = "ce.request.";
    public const string CommandPrefix = "ce.command.";

    public const string RequestWildcard = "ce.request.*";
    public const string CommandWildcard = "ce.command.*";

    /// <summary>
    /// Routing key for a request type. Strips a trailing "Request" from the type name
    /// before kebab-casing, so <c>SlashCommandInvokedRequest</c> -> <c>ce.request.slash-command-invoked</c>.
    /// </summary>
    public static string ForRequest(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return RequestPrefix + ToKebab(StripSuffix(messageType.Name, "Request"));
    }

    /// <summary>
    /// Routing key for a command type. Strips a trailing "Command" from the type name
    /// before kebab-casing, so <c>SendMessageCommand</c> -> <c>ce.command.send-message</c>.
    /// </summary>
    public static string ForCommand(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return CommandPrefix + ToKebab(StripSuffix(messageType.Name, "Command"));
    }

    private static string StripSuffix(string name, string suffix)
    {
        if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
        {
            return name[..^suffix.Length];
        }

        return name;
    }

    /// <summary>
    /// PascalCase -> kebab-case. Inserts a hyphen at every boundary where a lowercase
    /// or digit is followed by an uppercase, or where an uppercase is followed by an
    /// uppercase+lowercase (so "HTTPRequest" -> "http-request"). Result is fully lowercased.
    /// </summary>
    private static string ToKebab(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(pascalCase.Length + 8);
        for (var i = 0; i < pascalCase.Length; i++)
        {
            var c = pascalCase[i];

            if (i > 0 && char.IsUpper(c))
            {
                var prev = pascalCase[i - 1];
                var next = i + 1 < pascalCase.Length ? pascalCase[i + 1] : '\0';

                var prevIsLowerOrDigit = char.IsLower(prev) || char.IsDigit(prev);
                var endingAcronym = char.IsUpper(prev) && char.IsLower(next);

                if (prevIsLowerOrDigit || endingAcronym)
                {
                    sb.Append('-');
                }
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }
}
