using Autonogrammer.Workspace.Ignore;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests.Ignore;

/// <summary>The built-in list and the global list rank below every ignore file, and a <c>!</c> rule overrides them.</summary>
public sealed class IgnoreDefaultsTests
{
    private static IgnoreMatcher Matcher(IgnoreOptions? options = null) => IgnoreMatcher.Create(options ?? new IgnoreOptions { CaseSensitive = true });

    /// <summary>The built-in list hides lock files, environment files, version-control folders, caches, binaries, images, archives, fonts and keys, and nothing else.</summary>
    /// <param name="path">The path asked about.</param>
    /// <param name="expected">Whether the path is ignored.</param>
    [Theory]
    [InlineData(".env", true)]
    [InlineData(".env.local", true)]
    [InlineData("src/.env.production", true)]
    [InlineData(".env.example", true)]
    [InlineData("package-lock.json", true)]
    [InlineData("web/yarn.lock", true)]
    [InlineData("Cargo.lock", true)]
    [InlineData("go.sum", true)]
    [InlineData("node_modules/pkg/index.js", true)]
    [InlineData(".git/config", true)]
    [InlineData("app/__pycache__/x.pyc", true)]
    [InlineData("img/logo.png", true)]
    [InlineData("lib/native.dll", true)]
    [InlineData("dist.tar", true)]
    [InlineData("fonts/a.woff2", true)]
    [InlineData("keys/id_rsa", true)]
    [InlineData("keys/server.pem", true)]
    [InlineData("img/logo.svg", false)]
    [InlineData("src/main.rs", false)]
    [InlineData("src/app.cs", false)]
    [InlineData("README.md", false)]
    [InlineData("environment.md", false)]
    [InlineData(".github/workflows/ci.yml", false)]
    [InlineData(".gitignore", false)]
    public void TheBuiltInListHidesTheRightPaths(string path, bool expected) =>
        Matcher().IsIgnored(path, isDirectory: false).ShouldBe(expected);

    /// <summary>Switching the built-in list off lets its paths through.</summary>
    [Fact]
    public void TheBuiltInListCanBeSwitchedOff()
    {
        var matcher = Matcher(new IgnoreOptions { UseDefaultList = false });

        matcher.IsIgnored(".env", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("node_modules/a.js", isDirectory: false).ShouldBeFalse();
    }

    /// <summary>The built-in list is documented as data: it is not empty and every entry compiles.</summary>
    [Fact]
    public void EveryBuiltInPatternCompiles()
    {
        var matcher = Matcher();

        IgnoreDefaults.Patterns.ShouldNotBeEmpty();
        matcher.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>A <c>!</c> rule in an ignore file re-includes a built-in match, and only the named one.</summary>
    [Fact]
    public void ANegationOverridesTheBuiltInList()
    {
        var matcher = Matcher();
        matcher.AddFile(string.Empty, "!.env.example\n!package-lock.json\n");

        matcher.IsIgnored(".env.example", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("package-lock.json", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("web/package-lock.json", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored(".env", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("yarn.lock", isDirectory: false).ShouldBeTrue();
    }

    /// <summary>A negation in a subdirectory re-includes a built-in match inside that subtree only.</summary>
    [Fact]
    public void ANestedNegationOverridesTheBuiltInListInItsSubtree()
    {
        var matcher = Matcher();
        matcher.AddFile("sub", "!yarn.lock");

        matcher.IsIgnored("sub/yarn.lock", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("yarn.lock", isDirectory: false).ShouldBeTrue();
    }

    /// <summary>A negation cannot bring back a file below a built-in ignored folder.</summary>
    [Fact]
    public void ANegationCannotReachBelowAnIgnoredBuiltInFolder()
    {
        var matcher = Matcher();
        matcher.AddFile(string.Empty, "!node_modules/keep.js");

        matcher.IsIgnored("node_modules/keep.js", isDirectory: false).ShouldBeTrue();
    }

    /// <summary>The global list applies everywhere, ranks below an ignore file, and is empty by default.</summary>
    [Fact]
    public void TheGlobalListRanksBelowIgnoreFiles()
    {
        new IgnoreOptions().GlobalPatterns.ShouldBeEmpty();

        var matcher = Matcher(new IgnoreOptions { CaseSensitive = true, GlobalPatterns = ["*.secret", "!keep.secret"] });

        matcher.IsIgnored("a.secret", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("deep/er/a.secret", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("keep.secret", isDirectory: false).ShouldBeFalse();

        matcher.AddFile(string.Empty, "!a.secret");
        matcher.IsIgnored("a.secret", isDirectory: false).ShouldBeFalse();
    }

    /// <summary>A global rule beats the built-in list, so a global <c>!</c> re-includes a default match.</summary>
    [Fact]
    public void TheGlobalListRanksAboveTheBuiltInList()
    {
        var matcher = Matcher(new IgnoreOptions { CaseSensitive = true, GlobalPatterns = ["!.env.example"] });

        matcher.IsIgnored(".env.example", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored(".env", isDirectory: false).ShouldBeTrue();
    }

    /// <summary>A bad global rule is skipped and reported against the global list.</summary>
    [Fact]
    public void ABadGlobalRuleIsReported()
    {
        var matcher = Matcher(new IgnoreOptions { CaseSensitive = true, UseDefaultList = false, MaxPatternLength = 4, GlobalPatterns = ["ok", "waytoolong"] });

        matcher.IsIgnored("ok", isDirectory: false).ShouldBeTrue();
        matcher.Diagnostics.ShouldContain(new IgnoreDiagnostic("global ignore list", 2, "The rule is longer than 4 characters and was skipped."));
    }
}
