using CharacterEngineDiscord.Core.Configuration;
using CharacterEngineDiscord.Core.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.Tests.Core.Extensions;

public sealed class CoreServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfig()
    {
        var data = new Dictionary<string, string?>
        {
            ["Bot:Token"] = "test",
            ["Bot:PlayingStatus"] = "playing",
            ["Discord:MessageCacheSize"] = "10",
            ["Discord:ConnectionTimeoutMs"] = "30000",
            ["Admin:GuildId"] = "1",
            ["Admin:LogsChannelId"] = "2",
            ["Admin:ErrorsChannelId"] = "3",
            ["Admin:OwnerUserIds:0"] = "111",
            ["Admin:OwnerUserIds:1"] = "222",
            ["Messages:DefaultMessagesFormatFile"] = "fmt.txt",
            ["Messages:DefaultSystemPromptFile"] = "sys.txt",
            ["Messages:DefaultAvatarFile"] = "av.png",
            ["RateLimit:PerWindow"] = "5",
            ["RateLimit:FirstBlockMinutes"] = "10",
            ["RateLimit:SecondBlockHours"] = "1",
            ["Emoji:Sakura"] = ":s:",
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }

    [Fact]
    public void AddCharacterEngineCore_Should_Bind_BotOptions()
    {
        var services = new ServiceCollection();

        services.AddCharacterEngineCore(BuildConfig());

        using var provider = services.BuildServiceProvider();
        var bot = provider.GetRequiredService<IOptions<BotOptions>>().Value;

        bot.Token.Should().Be("test");
        bot.PlayingStatus.Should().Be("playing");
    }

    [Fact]
    public void AddCharacterEngineCore_Should_Bind_AdminOptions_With_OwnerUserIds_Array()
    {
        var services = new ServiceCollection();

        services.AddCharacterEngineCore(BuildConfig());

        using var provider = services.BuildServiceProvider();
        var admin = provider.GetRequiredService<IOptions<AdminOptions>>().Value;

        admin.GuildId.Should().Be(1ul);
        admin.LogsChannelId.Should().Be(2ul);
        admin.ErrorsChannelId.Should().Be(3ul);
        admin.OwnerUserIds.Should().HaveCount(2);
        admin.OwnerUserIds.Should().BeEquivalentTo(new ulong[] { 111ul, 222ul });
    }

    [Fact]
    public void AddCharacterEngineCore_Should_Bind_DiscordOptions()
    {
        var services = new ServiceCollection();

        services.AddCharacterEngineCore(BuildConfig());

        using var provider = services.BuildServiceProvider();
        var discord = provider.GetRequiredService<IOptions<DiscordOptions>>().Value;

        discord.MessageCacheSize.Should().Be(10);
        discord.ConnectionTimeoutMs.Should().Be(30000);
    }

    [Fact]
    public void AddCharacterEngineCore_Should_Register_BotOptionsValidator()
    {
        var services = new ServiceCollection();

        services.AddCharacterEngineCore(BuildConfig());

        using var provider = services.BuildServiceProvider();
        var validators = provider.GetServices<IValidateOptions<BotOptions>>().ToList();

        validators.Should().NotBeEmpty();
        validators.Should().Contain(v => v.GetType().Name == "BotOptionsValidator");
    }

    [Fact]
    public void AddCharacterEngineCore_Should_Register_AdminOptionsValidator()
    {
        var services = new ServiceCollection();

        services.AddCharacterEngineCore(BuildConfig());

        using var provider = services.BuildServiceProvider();
        var validators = provider.GetServices<IValidateOptions<AdminOptions>>().ToList();

        validators.Should().NotBeEmpty();
        validators.Should().Contain(v => v.GetType().Name == "AdminOptionsValidator");
    }

    [Fact]
    public void AddCharacterEngineCore_Should_Return_Same_Service_Collection_Instance()
    {
        var services = new ServiceCollection();

        var returned = services.AddCharacterEngineCore(BuildConfig());

        returned.Should().BeSameAs(services);
    }
}
