using CharacterEngineDiscord.DataAccess.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.DataAccess.Extensions;

/// <summary>
/// DI registration entry-point for everything in <c>CharacterEngineDiscord.DataAccess</c>.
/// </summary>
public static class DataAccessServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AppDbContext"/> against PostgreSQL with snake_case naming
    /// and the <see cref="CeDatabaseMigrationHostedService"/> startup hook.
    /// Connection string is read from the standard <c>ConnectionStrings:Default</c> section.
    /// </summary>
    public static IServiceCollection AddCharacterEngineDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name);
                npg.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });
            options.UseSnakeCaseNamingConvention();
        });

        services.AddHostedService<CeDatabaseMigrationHostedService>();

        return services;
    }
}
