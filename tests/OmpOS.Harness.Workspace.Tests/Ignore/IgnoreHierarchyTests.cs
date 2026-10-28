using OmpOS.Workspace.Ignore;
using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests.Ignore;

/// <summary>Ignore files in subdirectories apply to their subtree, deeper files override shallower ones, <c>.gitignore</c> is honoured, and <see cref="IgnoreMatcher.Explain"/> names the deciding rule.</summary>
public sealed class IgnoreHierarchyTests
{
    private static IgnoreOptions Plain(bool caseSensitive = true) => new() { CaseSensitive = caseSensitive, UseDefaultList = false };

    /// <summary>A deeper file re-includes what a shallower file ignored, inside its own subtree only.</summary>
    [Fact]
    public void ADeeperFileOverridesAShallowerOne()
    {
        var matcher = IgnoreMatcher.Create(Plain());
        matcher.AddFile(string.Empty, "*.log");
        matcher.AddFile("sub", "!*.log");

        matcher.IsIgnored("a.log", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("other/a.log", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("sub/a.log", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("sub/deep/a.log", isDirectory: false).ShouldBeFalse();
    }

    /// <summary>A shallower negation does not beat a deeper ignore.</summary>
    [Fact]
    public void ADeeperIgnoreBeatsAShallowerNegation()
    {
        var matcher = IgnoreMatcher.Create(Plain());
        matcher.AddFile(string.Empty, "!a.txt");
        matcher.AddFile("sub", "a.txt");

        matcher.IsIgnored("a.txt", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("sub/a.txt", isDirectory: false).ShouldBeTrue();
    }

    /// <summary>A nested file applies only below its directory, and an anchored rule in it is relative to that directory.</summary>
    [Fact]
    public void ANestedFileAppliesOnlyToItsSubtree()
    {
        var matcher = IgnoreMatcher.Create(Plain());
        matcher.AddFile("sub", "*.tmp\n/x");

        matcher.IsIgnored("sub/a.tmp", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("other/a.tmp", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("subway/a.tmp", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("sub/x", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("x", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("sub/y/x", isDirectory: false).ShouldBeFalse();
    }

    /// <summary>The directory of a nested file is matched with the case option and tolerates a trailing slash and dots.</summary>
    [Fact]
    public void TheDirectoryOfANestedFileFollowsTheCaseOption()
    {
        var insensitive = IgnoreMatcher.Create(Plain(caseSensitive: false));
        insensitive.AddFile("Sub/", "x");
        var sensitive = IgnoreMatcher.Create(Plain());
        sensitive.AddFile("Sub", "x");

        insensitive.IsIgnored("sub/x", isDirectory: false).ShouldBeTrue();
        sensitive.IsIgnored("sub/x", isDirectory: false).ShouldBeFalse();
        sensitive.IsIgnored("Sub/x", isDirectory: false).ShouldBeTrue();
    }

    /// <summary>A rule file whose directory climbs out of the workspace is skipped with a diagnostic.</summary>
    [Fact]
    public void AnUnsafeDirectoryAddsNothing()
    {
        var matcher = IgnoreMatcher.Create(Plain());

        matcher.AddFile("../outside", "*").ShouldBe(0);

        matcher.IsIgnored("a.txt", isDirectory: false).ShouldBeFalse();
        matcher.Diagnostics.Count.ShouldBe(1);
    }

    /// <summary>Loading reads <c>.gitignore</c> below the Cursor files, reads <c>.ompos/ignore</c>, and reads nested files.</summary>
    [Fact]
    public void LoadingReadsGitignoreAndTheConfiguredNames()
    {
        var source = new InMemoryIgnoreFileSource();
        source.Add(".gitignore", "*.gen\n*.keep\n");
        source.Add(".cursorignore", "!keep.gen\n");
        source.Add(".ompos/ignore", "*.omp\n");
        source.Add("pkg/.gitignore", "out/\n");

        var matcher = IgnoreMatcher.Load(source, ["", "pkg", "missing"], Plain());

        matcher.IsIgnored("x.gen", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("keep.gen", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("a.omp", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("pkg/out", isDirectory: true).ShouldBeTrue();
        matcher.IsIgnored("out", isDirectory: true).ShouldBeFalse();
        matcher.Diagnostics.ShouldBeEmpty();
        source.Read.ShouldContain("pkg/.ompos/ignore", StringComparer.Ordinal);
    }

    /// <summary>With <see cref="IgnoreOptions.RespectGitIgnore"/> off, <c>.gitignore</c> is not even read.</summary>
    [Fact]
    public void GitignoreCanBeSwitchedOff()
    {
        var source = new InMemoryIgnoreFileSource();
        source.Add(".gitignore", "*.gen\n");

        var matcher = IgnoreMatcher.Load(source, [string.Empty], Plain() with { RespectGitIgnore = false });

        matcher.IsIgnored("x.gen", isDirectory: false).ShouldBeFalse();
        source.Read.ShouldNotContain(".gitignore", StringComparer.Ordinal);
    }

    /// <summary>The ignore file names are an option: a custom list replaces the defaults.</summary>
    [Fact]
    public void CustomFileNamesReplaceTheDefaults()
    {
        var source = new InMemoryIgnoreFileSource();
        source.Add(".cursorignore", "*.a\n");
        source.Add(".myignore", "*.b\n");

        var matcher = IgnoreMatcher.Load(source, [string.Empty], Plain() with { RespectGitIgnore = false, IgnoreFileNames = [".myignore"] });

        matcher.IsIgnored("x.a", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("x.b", isDirectory: false).ShouldBeTrue();
    }

    /// <summary>Loading a directory reports whether any ignore file was found.</summary>
    [Fact]
    public void TryLoadHierarchyReportsWhetherAFileWasRead()
    {
        var source = new InMemoryIgnoreFileSource();
        source.Add("src/.cursorignore", "*.x\n");
        var matcher = IgnoreMatcher.Create(Plain());

        matcher.TryLoadHierarchy(source, "src").ShouldBeTrue();
        matcher.TryLoadHierarchy(source, "docs").ShouldBeFalse();
        matcher.TryLoadHierarchy(source, "../up").ShouldBeFalse();

        matcher.IsIgnored("src/a.x", isDirectory: false).ShouldBeTrue();
        matcher.Diagnostics.Count.ShouldBe(1);
    }

    /// <summary>An explanation names the deciding rule, its file and its line, also for a path under an ignored folder and for a re-including rule.</summary>
    [Fact]
    public void ExplainNamesTheDecidingRule()
    {
        var matcher = IgnoreMatcher.Create(Plain());
        matcher.AddFile("sub", "# comment\n*.log\n!keep.log\nbuild/\n");

        matcher.Explain("sub/a.log", isDirectory: false).ShouldBe(new IgnoreDecision(true, "*.log", "sub/.cursorignore", 2));
        matcher.Explain("sub/keep.log", isDirectory: false).ShouldBe(new IgnoreDecision(false, "!keep.log", "sub/.cursorignore", 3));
        matcher.Explain("sub/build/x/y.txt", isDirectory: false).ShouldBe(new IgnoreDecision(true, "build/", "sub/.cursorignore", 4));
        matcher.Explain("sub/a.txt", isDirectory: false).ShouldBe(new IgnoreDecision(false, null, null, 0));
        matcher.Explain(string.Empty, isDirectory: true).Ignored.ShouldBeFalse();
    }

    /// <summary>A caller-supplied source name shows up in the explanation.</summary>
    [Fact]
    public void ExplainShowsACustomSourceName()
    {
        var matcher = IgnoreMatcher.Create(Plain());
        matcher.AddFile(string.Empty, "secret.txt", "settings.json");

        matcher.Explain("secret.txt", isDirectory: false).Source.ShouldBe("settings.json");
    }

    /// <summary>Many threads can query a loaded matcher at once and get the same answers.</summary>
    [Fact]
    public async Task ConcurrentQueriesAgree()
    {
        var matcher = IgnoreMatcher.Create(Plain());
        matcher.AddFile(string.Empty, "*.log\nbuild/\n");
        matcher.AddFile("sub", "!keep.log");

        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(
            () =>
            {
                var correct = true;
                for (var round = 0; round < 200; round++)
                {
                    correct &= matcher.IsIgnored("a/b.log", isDirectory: false);
                    correct &= !matcher.IsIgnored("sub/keep.log", isDirectory: false);
                    correct &= matcher.IsIgnored("build/x/y", isDirectory: false);
                    correct &= !matcher.IsIgnored("src/main.rs", isDirectory: false);
                }

                return correct;
            },
            TestContext.Current.CancellationToken)).ToArray();

        var results = await Task.WhenAll(tasks).ConfigureAwait(true);

        results.ShouldAllBe(correct => correct);
    }
}
