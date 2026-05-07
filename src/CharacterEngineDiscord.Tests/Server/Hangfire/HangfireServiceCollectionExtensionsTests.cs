using CharacterEngineDiscord.Server.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.Tests.Server.Hangfire;

public sealed class HangfireServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCharacterEngineHangfire_Should_Register_Hangfire_Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=test;Username=x;Password=y",
            })
            .Build();

        services.AddCharacterEngineHangfire(config);

        services.Should().Contain(d => d.ServiceType.FullName!.StartsWith("Hangfire."));
    }

    [Fact]
    public void AddCharacterEngineHangfire_Should_Throw_When_Connection_String_Missing()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        var act = () => services.AddCharacterEngineHangfire(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Connection string 'Default' is not configured.*");
    }
}
