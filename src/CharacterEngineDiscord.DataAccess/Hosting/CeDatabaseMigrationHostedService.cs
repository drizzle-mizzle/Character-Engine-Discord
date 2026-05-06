using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.DataAccess.Hosting;

/// <summary>
/// Applies any pending EF Core migrations before the rest of the host starts.
/// Implemented as <see cref="IHostedService"/> (not <see cref="BackgroundService"/>) so
/// <see cref="StartAsync"/> blocks the Generic Host startup pipeline — services
/// registered after this one will not start until migrations succeed.
/// Fail-fast: an exception from <see cref="StartAsync"/> aborts host startup.
/// </summary>
internal sealed class CeDatabaseMigrationHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CeDatabaseMigrationHostedService> _logger;

    public CeDatabaseMigrationHostedService(IServiceScopeFactory scopeFactory, ILogger<CeDatabaseMigrationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Must be set before any DbContext is opened. Existing entities store DateTime.Now (Local kind)
        // and rely on the legacy timestamp behavior; new code should follow the same convention.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pending.Length == 0)
        {
            _logger.LogInformation("Database is up-to-date. No pending migrations.");
            return;
        }

        _logger.LogInformation("Applying {Count} pending migration(s): {Names}", pending.Length, string.Join(", ", pending));
        await db.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Migrations applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
