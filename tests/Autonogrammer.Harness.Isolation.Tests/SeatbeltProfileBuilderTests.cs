using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Isolation.Tests;

/// <summary>The Seatbelt profile matches the golden file, including a grant whose path contains a space.</summary>
public sealed class SeatbeltProfileBuilderTests
{
    /// <summary>Allow default, denied reads, allowed writes and quoted paths match the fixture.</summary>
    [Fact]
    public void GoldenProfileMatchesTheFixture()
    {
        var policy = IsolationPolicy.ForCowork(
            "/home/ada",
            "/tmp/scratch-session",
            [
                new IsolationGrant("/home/ada/project", true),
                new IsolationGrant("/home/ada/My Projects", true),
                new IsolationGrant("/home/ada/notes", false),
            ],
            "/opt/pi-agent",
            IsolationPlatform.MacOs);

        var profile = SeatbeltProfileBuilder.Build(policy);

        profile.ShouldBe(BubblewrapArgvBuilderTests.FixtureText("golden-seatbelt-profile.txt"));
        profile.Contains("(allow default)", StringComparison.Ordinal).ShouldBeTrue();
        profile.Contains("(deny file-write*)", StringComparison.Ordinal).ShouldBeTrue();
        profile.Contains("(allow file-write* (subpath \"/home/ada/My Projects\"))", StringComparison.Ordinal).ShouldBeTrue();
        profile.Contains("/home/ada/notes", StringComparison.Ordinal).ShouldBeFalse();
    }

    /// <summary>A path that contains a quote is escaped inside the scheme string.</summary>
    [Fact]
    public void QuoteEscapesQuotesAndBackslashes()
    {
        SeatbeltProfileBuilder.Quote("/tmp/a b").ShouldBe("\"/tmp/a b\"");
        SeatbeltProfileBuilder.Quote("a\"b\\c").ShouldBe("\"a\\\"b\\\\c\"");
    }
}
