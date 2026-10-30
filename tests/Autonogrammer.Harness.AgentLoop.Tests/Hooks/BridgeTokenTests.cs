using System.Buffers.Text;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>Verifies the bridge token: its length and alphabet, that no two are alike, and the comparison table.</summary>
public sealed class BridgeTokenTests
{
    private const string Token = "kQ3-Zr_9xP0aBcDeFgHiJkLmNoPqRsTuVwXyZ012345";

    [Fact]
    public void GeneratedTokenIsThirtyTwoRandomBytesAsUnpaddedBase64Url()
    {
        var token = BridgeToken.Generate();

        token.Length.ShouldBe(43);
        token.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').ShouldBeTrue();
        Base64Url.DecodeFromChars(token).Length.ShouldBe(32);
    }

    [Fact]
    public void ThousandGeneratedTokensAreAllDifferent()
    {
        var tokens = Enumerable.Range(0, 1000).Select(static _ => BridgeToken.Generate()).ToHashSet(StringComparer.Ordinal);

        tokens.Count.ShouldBe(1000);
    }

    [Fact]
    public void AGeneratedTokenMatchesItselfAndNoOtherGeneratedToken()
    {
        var first = BridgeToken.Generate();
        var second = BridgeToken.Generate();

        BridgeToken.Matches(first, first).ShouldBeTrue();
        BridgeToken.Matches(first, second).ShouldBeFalse();
    }

    [Theory]
    [InlineData(Token, Token, true)]
    [InlineData(Token, Token + "x", false)]
    [InlineData(Token, "x" + Token, false)]
    [InlineData(Token, "kQ3-Zr_9xP0aBcDeFgHiJkLmNoPqRsTuVwXyZ01234", false)]
    [InlineData(Token, "KQ3-ZR_9XP0ABCDEFGHIJKLMNOPQRSTUVWXYZ012345", false)]
    [InlineData(Token, "kQ3-Zr_9xP0aBcDeFgHiJkLmNoPqRsTuVwXyZ012346", false)]
    [InlineData(Token, "", false)]
    [InlineData(Token, null, false)]
    [InlineData(Token, " ", false)]
    [InlineData("", "", false)]
    [InlineData("", null, false)]
    [InlineData("", Token, false)]
    [InlineData("a", "a", true)]
    [InlineData("a", "b", false)]
    public void MatchesComparesTheWholeTokenCaseSensitively(string expected, string? presented, bool matches)
    {
        BridgeToken.Matches(expected, presented).ShouldBe(matches);
    }

    [Fact]
    public void MatchesNeverThrowsForAnInputThatIsNotValidText()
    {
        BridgeToken.Matches(Token, "\ud800").ShouldBeFalse();
        BridgeToken.Matches(Token, "\udfff").ShouldBeFalse();
        BridgeToken.Matches(null!, Token).ShouldBeFalse();
        BridgeToken.Matches(Token, new string('a', 100_000)).ShouldBeFalse();
    }
}
