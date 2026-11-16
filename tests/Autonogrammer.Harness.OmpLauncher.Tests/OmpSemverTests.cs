using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpLauncher.Tests;

public sealed class OmpSemverTests
{
    [Theory]
    [InlineData(">=1.4.4", "1.4.4", true)]
    [InlineData(">=1.4.4", "1.4.3", false)]
    [InlineData(">=1.4.4 <2.0.0", "2.0.0", false)]
    [InlineData(">=1.4.4 <2.0.0", "1.9.0", true)]
    [InlineData("1.4.4", "1.4.4", true)]
    [InlineData(">= 1.4.4", "1.5.0", true)]
    public void RangeChecksEveryComparator(string range, string version, bool expected)
    {
        OmpSemver.TryParse(version, out var parsed).ShouldBeTrue();

        OmpSemver.SatisfiesRange(range, parsed).ShouldBe((bool?)expected);
    }

    [Theory]
    [InlineData("^1.4.4")]
    [InlineData(">=1.4.4 || >=2.0.0")]
    [InlineData("*")]
    public void UnsupportedRangesReadAsUnknown(string range)
    {
        OmpSemver.TryParse("1.4.4", out var parsed).ShouldBeTrue();

        OmpSemver.SatisfiesRange(range, parsed).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.4")]
    [InlineData("1.4.4.1")]
    [InlineData("1.4.4+build")]
    [InlineData("../1.4.4")]
    [InlineData("1.4.4/..")]
    public void MalformedVersionsAreRejected(string text)
    {
        OmpSemver.TryParse(text, out _).ShouldBeFalse();
    }

    [Fact]
    public void ReleaseSortsAboveItsPrerelease()
    {
        OmpSemver.TryParse("1.0.0-beta", out var prerelease).ShouldBeTrue();
        OmpSemver.TryParse("1.0.0", out var release).ShouldBeTrue();

        OmpSemver.Compare(prerelease, release).ShouldBeLessThan(0);
    }
}
