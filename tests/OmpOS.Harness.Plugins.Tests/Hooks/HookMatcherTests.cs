using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies matcher semantics: unanchored regular expressions, match-everything forms and invalid expressions.
/// </summary>
public sealed class HookMatcherTests
{
    private static bool Matches(string? matcher, string? value)
    {
        HookMatcher.TryCompile(matcher, out var compiled, out _).ShouldBeTrue();
        return HookMatcher.Matches(matcher, compiled, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*")]
    [InlineData(" * ")]
    public void EmptyAndStarMatchEveryCallEvenWithoutAValue(string? matcher)
    {
        Matches(matcher, "anything").ShouldBeTrue();
        Matches(matcher, null).ShouldBeTrue();
        HookMatcher.MatchesEverything(matcher).ShouldBeTrue();
    }

    [Theory]
    [InlineData("curl|wget|nc ", "curl https://example.com", true)]
    [InlineData("curl|wget|nc ", "nc -l 80", true)]
    [InlineData("curl|wget|nc ", "ls -la", false)]
    [InlineData("explore|shell", "explore", true)]
    [InlineData("explore|shell", "generalPurpose", false)]
    [InlineData("Shell|Read|Write", "Read", true)]
    [InlineData("Shell", "MCP:linear", false)]
    [InlineData("^Shell$", "Shell", true)]
    [InlineData("^Shell$", "Shell2", false)]
    [InlineData("^MCP:", "MCP:linear_get", true)]
    public void MatchersAreUnanchoredRegularExpressions(string matcher, string value, bool expected)
    {
        Matches(matcher, value).ShouldBe(expected);
    }

    [Fact]
    public void ASpecificMatcherNeverMatchesAnEventWithoutAValue()
    {
        Matches("Shell", null).ShouldBeFalse();
    }

    [Fact]
    public void MatchingIsCaseSensitive()
    {
        Matches("shell", "Shell").ShouldBeFalse();
    }

    [Theory]
    [InlineData("([")]
    [InlineData("(?<")]
    [InlineData("a{2,1}")]
    public void AnInvalidExpressionIsReportedAndNeverMatches(string matcher)
    {
        HookMatcher.TryCompile(matcher, out var compiled, out var problem).ShouldBeFalse();

        problem.ShouldNotBeNullOrWhiteSpace();
        HookMatcher.Matches(matcher, compiled, "anything").ShouldBeFalse();
    }

    [Fact]
    public void ACatastrophicExpressionTimesOutAndDoesNotMatch()
    {
        HookMatcher.TryCompile("(a+)+$", out var compiled, out _).ShouldBeTrue();

        HookMatcher.Matches("(a+)+$", compiled, new string('a', 64) + "!").ShouldBeFalse();
    }
}
