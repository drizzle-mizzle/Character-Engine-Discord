using CharacterEngineDiscord.DataAccess;
using CharacterEngineDiscord.DataAccess.Extensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.Tests.DataAccess;

public sealed class DataAccessServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfig()
    {
        var data = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=ce_test;Username=postgres;Password=postgres",
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }

    [Fact]
    public void AddCharacterEngineDataAccess_Should_Configure_Npgsql_With_RetryingExecutionStrategy()
    {
        var services = new ServiceCollection();

        services.AddCharacterEngineDataAccess(BuildConfig());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DbContextOptions<AppDbContext>>();

        var relationalExt = options.Extensions.OfType<RelationalOptionsExtension>().Single();
        relationalExt.ExecutionStrategyFactory.Should().NotBeNull(
            "EnableRetryOnFailure must wire a retrying execution-strategy factory so transient Postgres failures don't crash the consumer loop");
    }

    [Fact]
    public void AddCharacterEngineDataAccess_Should_Throw_When_Connection_String_Missing()
    {
        var services = new ServiceCollection();
        var emptyConfig = new ConfigurationBuilder().Build();

        var act = () => services.AddCharacterEngineDataAccess(emptyConfig);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Connection string 'Default' is not configured.*");
    }
}
