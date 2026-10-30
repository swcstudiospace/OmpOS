using Autonogrammer.Harness.Automations.Hosted;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Automations.Tests;

/// <summary>Verifies the branch glob matcher: segment and cross-segment wildcards, single characters, case, and that no pattern is expensive.</summary>
public sealed class BranchGlobTests
{
    [Theory]
    [InlineData("main", "main", true)]
    [InlineData("main", "Main", false)]
    [InlineData("main", "main2", false)]
    [InlineData("", "", true)]
    [InlineData("", "main", false)]
    [InlineData("*", "", true)]
    [InlineData("*", "main", true)]
    [InlineData("*", "feature/x", false)]
    [InlineData("**", "feature/x/y", true)]
    [InlineData("feature/*", "feature/login", true)]
    [InlineData("feature/*", "feature/login/ui", false)]
    [InlineData("feature/*", "feature/", true)]
    [InlineData("feature/**", "feature/login/ui", true)]
    [InlineData("**/hotfix", "release/1.0/hotfix", true)]
    [InlineData("**/hotfix", "hotfix", false)]
    [InlineData("*/hotfix", "release/hotfix", true)]
    [InlineData("*/hotfix", "release/1.0/hotfix", false)]
    [InlineData("release-?.?", "release-1.2", true)]
    [InlineData("release-?.?", "release-10.2", false)]
    [InlineData("?", "/", false)]
    [InlineData("a*b*c", "aXXbYYc", true)]
    [InlineData("a*b*c", "aXXbYY", false)]
    [InlineData("v1.0", "v1x0", false)]
    [InlineData("a**b", "a/x/b", true)]
    [InlineData("a*b", "a/b", false)]
    public void Matches_follow_the_glob_rules(string pattern, string branch, bool expected)
    {
        BranchGlob.IsMatch(pattern, branch).ShouldBe(expected);
    }

    [Fact]
    public void A_pathological_pattern_is_answered_at_once()
    {
        var pattern = string.Concat(Enumerable.Repeat("*a", 40)) + "b";
        var text = new string('a', 200);

        BranchGlob.IsMatch(pattern, text).ShouldBeFalse();
        BranchGlob.IsMatch(pattern, text + "b").ShouldBeTrue();
    }

    [Fact]
    public void An_empty_filter_list_accepts_every_branch_and_a_list_accepts_any_match()
    {
        BranchGlob.MatchesAnyOrNoFilter([], "whatever").ShouldBeTrue();
        BranchGlob.MatchesAnyOrNoFilter(["main", "release/*"], "release/1").ShouldBeTrue();
        BranchGlob.MatchesAnyOrNoFilter(["main", "release/*"], "develop").ShouldBeFalse();
    }
}
