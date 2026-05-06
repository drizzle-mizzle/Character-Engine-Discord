using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.Core.Configuration.Validators;

/// <summary>
/// Imperative checks for <see cref="BotOptions"/> beyond simple data-annotations.
/// </summary>
internal sealed class BotOptionsValidator : IValidateOptions<BotOptions>
{
    public ValidateOptionsResult Validate(string? name, BotOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            failures.Add($"{nameof(BotOptions.Token)} must not be null or whitespace.");
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }
}
