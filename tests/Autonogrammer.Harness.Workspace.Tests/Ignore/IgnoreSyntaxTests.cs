using Autonogrammer.Workspace.Ignore;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests.Ignore;

/// <summary>The rule syntax follows gitignore: anchoring, folder-only rules, negation, <c>**</c>, character classes, escapes, comments and trailing spaces.</summary>
public sealed class IgnoreSyntaxTests
{
    private static IgnoreMatcher Matcher(string rules, bool caseSensitive = true)
    {
        var matcher = IgnoreMatcher.Create(new IgnoreOptions { CaseSensitive = caseSensitive, UseDefaultList = false, RespectGitIgnore = false });
        matcher.AddFile(string.Empty, rules);
        return matcher;
    }

    /// <summary>One rule file and one path give the answer gitignore gives.</summary>
    /// <param name="rules">The ignore file text.</param>
    /// <param name="path">The path asked about.</param>
    /// <param name="isDirectory">Whether the path is a folder.</param>
    /// <param name="expected">Whether the path is ignored.</param>
    [Theory]
    [InlineData("*.log", "a.log", false, true)]
    [InlineData("*.log", "dir/sub/a.log", false, true)]
    [InlineData("*.log", "a.txt", false, false)]
    [InlineData("/a.txt", "a.txt", false, true)]
    [InlineData("/a.txt", "dir/a.txt", false, false)]
    [InlineData("dir/a.txt", "dir/a.txt", false, true)]
    [InlineData("dir/a.txt", "x/dir/a.txt", false, false)]
    [InlineData("doc/*.txt", "doc/a.txt", false, true)]
    [InlineData("doc/*.txt", "doc/sub/a.txt", false, false)]
    [InlineData("build/", "build", true, true)]
    [InlineData("build/", "build", false, false)]
    [InlineData("build/", "build/out.o", false, true)]
    [InlineData("build/", "src/build", true, true)]
    [InlineData("build/", "src/build/deep/file.txt", false, true)]
    [InlineData("/build/", "build", true, true)]
    [InlineData("/build/", "src/build", true, false)]
    [InlineData("*.log\n!keep.log", "keep.log", false, false)]
    [InlineData("*.log\n!keep.log", "other.log", false, true)]
    [InlineData("!keep.log\n*.log", "keep.log", false, true)]
    [InlineData("build/\n!build/keep.txt", "build/keep.txt", false, true)]
    [InlineData("*\n!src/\n!src/*.cs", "src/a.cs", false, false)]
    [InlineData("*\n!src/\n!src/*.cs", "src/a.txt", false, true)]
    [InlineData("*\n!src/a.cs", "src/a.cs", false, true)]
    [InlineData("**/x", "x", false, true)]
    [InlineData("**/x", "a/b/x", false, true)]
    [InlineData("**/x", "a/y", false, false)]
    [InlineData("x/**", "x/a", false, true)]
    [InlineData("x/**", "x/a/b", false, true)]
    [InlineData("x/**", "x", true, false)]
    [InlineData("a/**/b", "a/b", false, true)]
    [InlineData("a/**/b", "a/x/y/b", false, true)]
    [InlineData("a/**/b", "x/a/b", false, false)]
    [InlineData("a/**/b", "a/c", false, false)]
    [InlineData("**", "any/where.txt", false, true)]
    [InlineData("a**b", "axxb", false, true)]
    [InlineData("a**b", "a/b", false, false)]
    [InlineData("a?c", "abc", false, true)]
    [InlineData("a?c", "ac", false, false)]
    [InlineData("a?c", "a/c", false, false)]
    [InlineData("[abc].txt", "b.txt", false, true)]
    [InlineData("[abc].txt", "d.txt", false, false)]
    [InlineData("[a-c].txt", "c.txt", false, true)]
    [InlineData("[a-c].txt", "e.txt", false, false)]
    [InlineData("[!abc].txt", "d.txt", false, true)]
    [InlineData("[!abc].txt", "a.txt", false, false)]
    [InlineData("[]a].txt", "].txt", false, true)]
    [InlineData("[abc", "[abc", false, true)]
    [InlineData("[abc", "a", false, false)]
    [InlineData("\\#file", "#file", false, true)]
    [InlineData("\\!important", "!important", false, true)]
    [InlineData("a\\*b", "a*b", false, true)]
    [InlineData("a\\*b", "axb", false, false)]
    [InlineData("#*.log", "a.log", false, false)]
    [InlineData("\n\n*.log\n\n", "a.log", false, true)]
    [InlineData("*.log\r\n*.tmp\r\n", "a.tmp", false, true)]
    [InlineData("\uFEFF*.log", "a.log", false, true)]
    [InlineData("foo   ", "foo", false, true)]
    [InlineData("foo\\ ", "foo ", false, true)]
    [InlineData("foo\\ ", "foo", false, false)]
    [InlineData(" foo", " foo", false, true)]
    [InlineData(" foo", "foo", false, false)]
    public void GitignoreSemanticsHold(string rules, string path, bool isDirectory, bool expected) =>
        Matcher(rules).IsIgnored(path, isDirectory).ShouldBe(expected);

    /// <summary>Case rules apply to literals, wildcards, classes, anchored rules and folder names.</summary>
    /// <param name="rules">The ignore file text.</param>
    /// <param name="path">The path asked about.</param>
    /// <param name="caseSensitive">The case option.</param>
    /// <param name="expected">Whether the path is ignored.</param>
    [Theory]
    [InlineData("*.LOG", "a.log", true, false)]
    [InlineData("*.LOG", "a.log", false, true)]
    [InlineData("README.md", "readme.MD", true, false)]
    [InlineData("README.md", "readme.MD", false, true)]
    [InlineData("[A-C].txt", "b.txt", true, false)]
    [InlineData("[A-C].txt", "b.txt", false, true)]
    [InlineData("/Src/", "src/a.cs", true, false)]
    [InlineData("/Src/", "src/a.cs", false, true)]
    public void CaseOptionDecidesWhetherCaseMatters(string rules, string path, bool caseSensitive, bool expected) =>
        Matcher(rules, caseSensitive).IsIgnored(path, isDirectory: false).ShouldBe(expected);

    /// <summary>A rooted path, a climbing path or a path with a control character is reported as ignored; the root and dotted paths inside it are not.</summary>
    /// <param name="path">The path asked about.</param>
    /// <param name="expected">Whether the path is ignored.</param>
    [Theory]
    [InlineData("../x", true)]
    [InlineData("a/../b", true)]
    [InlineData("a\\..\\b", true)]
    [InlineData("/etc/passwd", true)]
    [InlineData("\\\\server\\share", true)]
    [InlineData("C:/x", true)]
    [InlineData("a\0b", true)]
    [InlineData("./a/b.txt", false)]
    [InlineData("a//b.txt", false)]
    [InlineData("", false)]
    public void UnsafePathsAreIgnoredAndTheRootIsNot(string path, bool expected) =>
        Matcher("nothing-matches-this").IsIgnored(path, isDirectory: false).ShouldBe(expected);

    /// <summary>A path deeper than the cap is reported as ignored; one at the cap is evaluated normally.</summary>
    [Fact]
    public void ADeepPathBeyondTheCapIsIgnored()
    {
        var matcher = IgnoreMatcher.Create(new IgnoreOptions { MaxPathDepth = 3, UseDefaultList = false });

        matcher.IsIgnored("a/b/c", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("a/b/c/d", isDirectory: false).ShouldBeTrue();
        matcher.Explain("a/b/c/d", isDirectory: false).Rule.ShouldBe("(unsafe path)");
    }

    /// <summary>A null option set is refused.</summary>
    [Fact]
    public void NullOptionsAreRefused() =>
        Should.Throw<ArgumentNullException>(() => { _ = IgnoreMatcher.Create(null!); });
}
