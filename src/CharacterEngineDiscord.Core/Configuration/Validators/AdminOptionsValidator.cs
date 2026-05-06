using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.Core.Configuration.Validators;

/// <summary>
/// Imperative checks for <see cref="AdminOptions"/> beyond simple data-annotations.
/// </summary>
internal sealed class AdminOptionsValidator : IValidateOptions<AdminOptions>
{
    public ValidateOptionsResult Validate(string? name, AdminOptions options)
    {
        var failures = new List<string>();

        if (options.GuildId == 0)
        {
            failures.Add($"{nameof(AdminOptions.GuildId)} must be greater than 0.");
        }

        if (options.LogsChannelId == 0)
        {
            failures.Add($"{nameof(AdminOptions.LogsChannelId)} must be greater than 0.");
        }

        if (options.ErrorsChannelId == 0)
        {
            failures.Add($"{nameof(AdminOptions.ErrorsChannelId)} must be greater than 0.");
        }

        if (options.OwnerUserIds is null || options.OwnerUserIds.Length == 0)
        {
            failures.Add($"{nameof(AdminOptions.OwnerUserIds)} must contain at least one user id.");
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }
}
