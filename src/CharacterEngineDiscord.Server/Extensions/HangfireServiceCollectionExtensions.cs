using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.Server.Extensions;

/// <summary>
/// DI registration for Hangfire (LGPL-3.0) using the existing PostgreSQL connection.
/// Hangfire creates its own <c>hangfire.*</c> schema on first run, isolated from EF
/// migrations that own the <c>public</c> schema. Concrete jobs live under
/// <see cref="Jobs"/> and are wired via <see cref="IBackgroundJobClient"/> /
/// <see cref="IRecurringJobManager"/> in future features.
/// </summary>
public static class HangfireServiceCollectionExtensions
{
    public static IServiceCollection AddCharacterEngineHangfire(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddHangfire(global =>
        {
            global
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(connectionString));
        });

        services.AddHangfireServer(serverOptions =>
        {
            // Sensible defaults; tune later if needed.
            serverOptions.WorkerCount = Math.Max(Environment.ProcessorCount, 4);
            serverOptions.Queues = ["default"];
        });

        return services;
    }
}
