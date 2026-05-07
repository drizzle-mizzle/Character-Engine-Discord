using CharacterEngineDiscord.Core.Abstractions.Time;
using CharacterEngineDiscord.Core.Configuration;
using CharacterEngineDiscord.Core.Configuration.Validators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.Core.Extensions;

/// <summary>
/// DI registration entry-point for everything in <c>CharacterEngineDiscord.Core</c>.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Binds every Core options POCO to its configuration section, attaches
    /// data-annotation validation and the custom <see cref="IValidateOptions{TOptions}"/> validators,
    /// and forces validation at host start.
    /// </summary>
    public static IServiceCollection AddCharacterEngineCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BotOptions>()
                .Bind(configuration.GetSection("Bot"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<DiscordOptions>()
                .Bind(configuration.GetSection("Discord"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<AdminOptions>()
                .Bind(configuration.GetSection("Admin"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<MessagesOptions>()
                .Bind(configuration.GetSection("Messages"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<RateLimitOptions>()
                .Bind(configuration.GetSection("RateLimit"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<EmojiOptions>()
                .Bind(configuration.GetSection("Emoji"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddSingleton<IValidateOptions<BotOptions>, BotOptionsValidator>();
        services.AddSingleton<IValidateOptions<AdminOptions>, AdminOptionsValidator>();

        // SystemClock is internal to the Core assembly, so direct type-mapping cannot
        // resolve it cross-assembly; an explicit factory keeps the type internal while
        // exposing only the IClock contract.
        services.AddSingleton<IClock>(_ => new SystemClock());

        return services;
    }
}
