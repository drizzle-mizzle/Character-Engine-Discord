using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CharacterEngineDiscord.DataAccess;

/// <summary>
/// Used only by the EF Core CLI (<c>dotnet ef migrations add</c>, <c>dotnet ef migrations script</c>, etc).
/// At design-time the runtime DI container is not available, so we build a minimal
/// <see cref="AppDbContext"/> with placeholder Npgsql options. No real connection is opened —
/// scaffolding queries the model, not the database.
/// </summary>
internal sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=ce_design_time;Username=postgres;Password=postgres",
                npg =>
                {
                    npg.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name);
                    npg.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                })
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
