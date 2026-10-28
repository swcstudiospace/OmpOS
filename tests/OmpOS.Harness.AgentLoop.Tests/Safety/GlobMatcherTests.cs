using OmpOS.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Safety;

/// <summary>The glob semantics of the tokens: one level for <c>*</c>, any depth for <c>**</c>, one character for <c>?</c>.</summary>
public sealed class GlobMatcherTests
{
    [Theory]
    [InlineData("/w/src/**", "/w/src/a.ts", true)]
    [InlineData("/w/src/**", "/w/src/a/b/c.ts", true)]
    [InlineData("/w/src/**", "/w/src", true)]
    [InlineData("/w/src/**", "/w/srcx/a.ts", false)]
    [InlineData("/w/src/*", "/w/src/a.ts", true)]
    [InlineData("/w/src/*", "/w/src/a/b.ts", false)]
    [InlineData("/w/**/*.ts", "/w/a.ts", true)]
    [InlineData("/w/**/*.ts", "/w/a/b/c.ts", true)]
    [InlineData("/w/**/*.ts", "/w/a/b/c.js", false)]
    [InlineData("/w/**/.env*", "/w/.env", true)]
    [InlineData("/w/**/.env*", "/w/pkg/.env.local", true)]
    [InlineData("/w/.env*", "/w/pkg/.env", false)]
    [InlineData("/w/a?c", "/w/abc", true)]
    [InlineData("/w/a?c", "/w/ac", false)]
    [InlineData("/w/a*c", "/w/abbbc", true)]
    [InlineData("/w/a*c", "/w/a/c", false)]
    [InlineData("/w/**", "/other/a", false)]
    [InlineData("/w/a", "/w/a/b", false)]
    [InlineData("/w/a/**/b/**/c", "/w/a/x/b/y/z/c", true)]
    [InlineData("/w/a/**/b/**/c", "/w/a/b/c", true)]
    [InlineData("/w/a/**/b/**/c", "/w/a/x/c", false)]
    public void APathIsMatchedByItsPattern(string pattern, string path, bool expected)
    {
        GlobMatcher.MatchPath(pattern, path, ignoreCase: false).ShouldBe(expected);
    }

    [Fact]
    public void CaseIsIgnoredOnlyWhenAsked()
    {
        GlobMatcher.MatchPath("/w/README.md", "/w/readme.md", ignoreCase: false).ShouldBeFalse();
        GlobMatcher.MatchPath("/w/README.md", "/w/readme.md", ignoreCase: true).ShouldBeTrue();
    }

    [Theory]
    [InlineData("github:*", "github:list_issues", true)]
    [InlineData("*", "", true)]
    [InlineData("*.example.com", "docs.example.com", true)]
    [InlineData("*.example.com", "a.b.example.com", true)]
    [InlineData("*.example.com", "example.com", false)]
    [InlineData("*.example.com", "badexample.com", false)]
    [InlineData("list_*", "list_users", true)]
    [InlineData("list_*", "xlist_users", false)]
    [InlineData("a*b*c", "aXXbYYc", true)]
    [InlineData("a*b*c", "aXXbYY", false)]
    [InlineData("?", "ab", false)]
    public void AWildcardMatchesAnyRunOfCharacters(string pattern, string text, bool expected)
    {
        GlobMatcher.Wildcard(pattern, text, ignoreCase: false).ShouldBe(expected);
    }

    [Fact]
    public void ALiteralPatternIsOneWithNoWildcard()
    {
        GlobMatcher.IsLiteral("/a/b").ShouldBeTrue();
        GlobMatcher.IsLiteral("/a/*").ShouldBeFalse();
        GlobMatcher.IsLiteral("/a/?").ShouldBeFalse();
    }
}
