using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Tests for <see cref="RailwayServiceLabel"/>: sanitising, the length cap and collision safety.
/// </summary>
public sealed class RailwayServiceLabelTests
{
    private const string Prefix = "ompos-agent-";

    [Fact]
    public void ASimpleLowercaseIdIsUsedAsIs()
    {
        RailwayServiceLabel.Create(Prefix, "s1").ShouldBe("ompos-agent-s1");
        RailwayServiceLabel.Create(Prefix, "abc-123").ShouldBe("ompos-agent-abc-123");
    }

    [Fact]
    public void AnAlteredIdGetsAHashSuffixSoItNeverCollidesWithItsSanitisedForm()
    {
        var upper = RailwayServiceLabel.Create(Prefix, "Session-A");
        var lower = RailwayServiceLabel.Create(Prefix, "session-a");
        var underscore = RailwayServiceLabel.Create(Prefix, "a_b");
        var dash = RailwayServiceLabel.Create(Prefix, "a-b");

        lower.ShouldBe("ompos-agent-session-a");
        upper.ShouldStartWith("ompos-agent-session-a-", Case.Sensitive);
        upper.Length.ShouldBe("ompos-agent-session-a-".Length + 8);
        upper.ShouldNotBe(lower, StringComparer.Ordinal);
        underscore.ShouldNotBe(dash, StringComparer.Ordinal);
        underscore.ShouldStartWith("ompos-agent-a-b-", Case.Sensitive);
    }

    [Fact]
    public void LabelsContainOnlyLowercaseLettersDigitsAndDashesAndAreDeterministic()
    {
        string[] ids = ["Ünïcode session", "a b\tc", "UPPER", "x/y\\z", "ok", new string('Z', 200)];

        foreach (var id in ids)
        {
            var label = RailwayServiceLabel.Create(Prefix, id);

            label.ShouldAllBe(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-');
            label.Length.ShouldBeLessThanOrEqualTo(RailwayServiceLabel.MaxLength);
            RailwayServiceLabel.Create(Prefix, id).ShouldBe(label);
        }
    }

    [Fact]
    public void LongIdsThatShareALongPrefixStayDistinctAndWithinTheCap()
    {
        var shared = new string('a', 100);
        var first = RailwayServiceLabel.Create(Prefix, shared + "-one");
        var second = RailwayServiceLabel.Create(Prefix, shared + "-two");

        first.ShouldNotBe(second, StringComparer.Ordinal);
        first.Length.ShouldBeLessThanOrEqualTo(RailwayServiceLabel.MaxLength);
        second.Length.ShouldBeLessThanOrEqualTo(RailwayServiceLabel.MaxLength);
        first.ShouldStartWith(Prefix, Case.Sensitive);
        first.ShouldNotEndWith("--", Case.Sensitive);
    }

    [Fact]
    public void AnIdAtTheLimitIsKeptAndOneCharacterOverIsHashed()
    {
        var fits = new string('a', RailwayServiceLabel.MaxLength - Prefix.Length);
        var over = fits + "a";

        RailwayServiceLabel.Create(Prefix, fits).ShouldBe(Prefix + fits);
        var hashed = RailwayServiceLabel.Create(Prefix, over);
        hashed.Length.ShouldBe(RailwayServiceLabel.MaxLength);
        hashed.ShouldNotBe(Prefix + fits, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("---")]
    [InlineData("ñ")]
    public void AnIdWithNoLetterOrDigitIsRejected(string id)
    {
        Should.Throw<ArgumentException>(() => RailwayServiceLabel.Create(Prefix, id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AnEmptyIdIsRejected(string id)
    {
        Should.Throw<ArgumentException>(() => RailwayServiceLabel.Create(Prefix, id));
    }
}
