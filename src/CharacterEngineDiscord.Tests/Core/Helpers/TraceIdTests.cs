using System.Text.RegularExpressions;
using CharacterEngineDiscord.Core.Helpers;
using FluentAssertions;

namespace CharacterEngineDiscord.Tests.Core.Helpers;

public sealed class TraceIdTests
{
    [Fact]
    public void New_Should_Return_String_Of_Length_8()
    {
        var id = TraceId.New();

        id.Should().NotBeNull();
        id.Should().HaveLength(8);
    }

    [Fact]
    public void New_Should_Return_Lowercase_Hex_Only()
    {
        for (var i = 0; i < 50; i++)
        {
            var id = TraceId.New();

            Regex.IsMatch(id, "^[0-9a-f]{8}$").Should().BeTrue($"id '{id}' must be 8 lowercase hex chars");
        }
    }

    [Fact]
    public void New_Should_Produce_Mostly_Unique_Ids_Over_1000_Calls()
    {
        var ids = new HashSet<string>(capacity: 1000);

        for (var i = 0; i < 1000; i++)
        {
            ids.Add(TraceId.New());
        }

        ids.Count.Should().BeGreaterThanOrEqualTo(990);
    }
}
