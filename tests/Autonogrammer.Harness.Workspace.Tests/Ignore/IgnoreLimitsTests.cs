using Autonogrammer.Workspace.Ignore;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests.Ignore;

/// <summary>Over-cap lines and files, symbolic links and adversarial patterns are skipped, reported or bounded; nothing throws and nothing hangs.</summary>
public sealed class IgnoreLimitsTests
{
    private static readonly int[] SkippedLines = [4, 5];

    private static IgnoreOptions Plain() => new() { CaseSensitive = true, UseDefaultList = false, RespectGitIgnore = false };

    /// <summary>A file with more rules than the cap keeps the first rules and reports the cut once.</summary>
    [Fact]
    public void ARuleBeyondTheLimitIsSkippedAndReported()
    {
        var matcher = IgnoreMatcher.Create(Plain() with { MaxRulesPerFile = 2 });

        matcher.AddFile(string.Empty, "a\nb\nc\nd").ShouldBe(2);

        matcher.IsIgnored("a", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("b", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("c", isDirectory: false).ShouldBeFalse();
        matcher.Diagnostics.Count.ShouldBe(1);
        matcher.Diagnostics[0].ShouldBe(new IgnoreDiagnostic(".cursorignore", 3, "The file has more than 2 rules; this one and the rest were skipped."));
    }

    /// <summary>A rule longer than the cap is skipped with its line number; its neighbours still apply.</summary>
    [Fact]
    public void AnOverlongRuleIsSkippedAndReported()
    {
        var matcher = IgnoreMatcher.Create(Plain() with { MaxPatternLength = 5 });

        matcher.AddFile(string.Empty, "short\nwaytoolongpattern\nok").ShouldBe(2);

        matcher.IsIgnored("ok", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("waytoolongpattern", isDirectory: false).ShouldBeFalse();
        matcher.Diagnostics.Count.ShouldBe(1);
        matcher.Diagnostics[0].ShouldBe(new IgnoreDiagnostic(".cursorignore", 2, "The rule is longer than 5 characters and was skipped."));
    }

    /// <summary>A line that holds no pattern (a lone <c>!</c> or <c>/</c>) is reported, while blank lines and comments are silent.</summary>
    [Fact]
    public void ALineWithoutAPatternIsReported()
    {
        var matcher = IgnoreMatcher.Create(Plain());

        matcher.AddFile("sub", "\n# note\n   \n!\n/\n*.x").ShouldBe(1);

        matcher.Diagnostics.Select(diagnostic => diagnostic.Line).ToArray().ShouldBe(SkippedLines);
        matcher.Diagnostics.All(diagnostic => string.Equals(diagnostic.Source, "sub/.cursorignore", StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>A file over the size cap is skipped with a diagnostic and none of its rules apply.</summary>
    [Fact]
    public void AnOversizeFileIsSkippedAndReported()
    {
        var source = new InMemoryIgnoreFileSource();
        source.Add(".cursorignore", "*.log\n" + new string('#', 100));

        var matcher = IgnoreMatcher.Load(source, [string.Empty], Plain() with { MaxFileBytes = 16 });

        matcher.IsIgnored("a.log", isDirectory: false).ShouldBeFalse();
        matcher.Diagnostics.Count.ShouldBe(1);
        matcher.Diagnostics[0].ShouldBe(new IgnoreDiagnostic(".cursorignore", 0, "The ignore file is larger than 16 bytes and was skipped."));
    }

    /// <summary>The size cap counts UTF-8 bytes, not characters.</summary>
    [Fact]
    public void TheSizeCapCountsBytes()
    {
        var source = new InMemoryIgnoreFileSource();
        source.Add(".cursorignore", "\u00e9\u00e9\u00e9\u00e9\u00e9\u00e9");

        var matcher = IgnoreMatcher.Load(source, [string.Empty], Plain() with { MaxFileBytes = 8 });

        matcher.Diagnostics.Count.ShouldBe(1);
    }

    /// <summary>An ignore file that is a symbolic link is not followed; the matcher reports it and applies none of its rules.</summary>
    [Fact]
    public void ASymbolicLinkedIgnoreFileIsSkippedAndReported()
    {
        var source = new InMemoryIgnoreFileSource();
        source.AddSymbolicLink(".cursorignore", "*.log\n");

        var matcher = IgnoreMatcher.Load(source, [string.Empty], Plain());

        matcher.IsIgnored("a.log", isDirectory: false).ShouldBeFalse();
        matcher.Diagnostics.Count.ShouldBe(1);
        matcher.Diagnostics[0].Source.ShouldBe(".cursorignore");
        matcher.Diagnostics[0].Message.ShouldContain("symbolic link", Case.Sensitive);
        source.Read.ShouldNotContain(".cursorignore", StringComparer.Ordinal);
    }

    /// <summary>A pattern built to make a backtracking matcher explode finishes at once and does not match.</summary>
    [Fact]
    public void APathologicalPatternFinishesWithinTheWorkCap()
    {
        var matcher = IgnoreMatcher.Create(Plain());
        matcher.AddFile(string.Empty, "a*a*a*a*a*a*a*a*b\n**/a*a*a*a*a*a*b");
        var name = new string('a', 20_000);

        matcher.IsIgnored(name, isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("x/" + name, isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored(name + "b", isDirectory: false).ShouldBeTrue();
    }

    /// <summary>A rule that runs out of its step budget counts as matching, so an adversarial pattern can hide a file but never expose one.</summary>
    [Fact]
    public void ARuleThatExhaustsItsBudgetFailsClosed()
    {
        var matcher = IgnoreMatcher.Create(Plain() with { MaxMatchWork = 8 });
        matcher.AddFile(string.Empty, "x*y");
        var longName = new string('x', 200);

        matcher.IsIgnored(longName, isDirectory: false).ShouldBeTrue();
        matcher.Explain(longName, isDirectory: false).Rule.ShouldBe("x*y");
        matcher.IsIgnored("xy", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("zz", isDirectory: false).ShouldBeFalse();
    }

    /// <summary>A re-including rule that runs out of budget does not re-include.</summary>
    [Fact]
    public void ANegationThatExhaustsItsBudgetDoesNotReInclude()
    {
        var longName = new string('x', 200);
        var rules = longName + "\n!x*x";
        var unbounded = IgnoreMatcher.Create(Plain());
        unbounded.AddFile(string.Empty, rules);
        var bounded = IgnoreMatcher.Create(Plain() with { MaxMatchWork = 8 });
        bounded.AddFile(string.Empty, rules);

        unbounded.IsIgnored(longName, isDirectory: false).ShouldBeFalse();
        bounded.IsIgnored(longName, isDirectory: false).ShouldBeTrue();
    }
}
