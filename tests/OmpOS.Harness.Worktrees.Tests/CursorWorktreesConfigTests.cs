using Shouldly;
using Xunit;

namespace OmpOS.Worktrees.Tests;

/// <summary>
/// Verifies reading <c>.cursor/worktrees.json</c>: the three keys, command arrays versus script paths, OS precedence, the
/// worktree-then-project lookup order, and the rejection of malformed files.
/// </summary>
public sealed class CursorWorktreesConfigTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ompos-cursor-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "project", ".cursor", "worktrees.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string WriteConfig(string folder, string json)
    {
        var path = Path.Combine(_directory, folder, ".cursor", "worktrees.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return Path.Combine(_directory, folder);
    }

    [Fact]
    public void ReadsCommandArraysAndAScriptPathResolvedAgainstTheFile()
    {
        const string json = """
            {
              // install, then copy the secrets from the main checkout
              "setup-worktree": [ "npm ci", "cp $ROOT_WORKTREE_PATH/.env .env", ],
              "setup-worktree-unix": "scripts/setup.sh",
              "setup-worktree-windows": ["npm ci"],
              "something-new": { "ignored": true }
            }
            """;

        var config = CursorWorktreesConfig.Parse(json, FilePath);

        var generic = config.Generic.ShouldNotBeNull();
        generic.Commands.ShouldBe(["npm ci", "cp $ROOT_WORKTREE_PATH/.env .env"]);
        generic.IsScript.ShouldBeFalse();
        var unix = config.Unix.ShouldNotBeNull();
        unix.IsScript.ShouldBeTrue();
        unix.ScriptPath.ShouldBe(Path.Combine(_directory, "project", ".cursor", "scripts", "setup.sh"));
        unix.Commands.ShouldBeEmpty();
        config.Windows.ShouldNotBeNull().Commands.ShouldBe(["npm ci"]);
        config.FilePath.ShouldBe(FilePath);
    }

    [Fact]
    public void TheOperatingSystemKeyTakesPrecedenceOverTheGenericOneAndTheGenericOneIsTheFallback()
    {
        var both = CursorWorktreesConfig.Parse("""{ "setup-worktree": ["generic"], "setup-worktree-unix": ["unix"], "setup-worktree-windows": ["win"] }""", FilePath);
        var genericOnly = CursorWorktreesConfig.Parse("""{ "setup-worktree": ["generic"] }""", FilePath);
        var none = CursorWorktreesConfig.Parse("{ }", FilePath);

        both.SetupFor(windows: false).ShouldNotBeNull().Commands.ShouldBe(["unix"]);
        both.SetupFor(windows: true).ShouldNotBeNull().Commands.ShouldBe(["win"]);
        genericOnly.SetupFor(windows: true).ShouldNotBeNull().Commands.ShouldBe(["generic"]);
        genericOnly.SetupFor(windows: false).ShouldNotBeNull().Commands.ShouldBe(["generic"]);
        none.SetupFor(windows: false).ShouldBeNull();
    }

    [Fact]
    public void FindPrefersTheWorktreeCopyOverTheProjectRootCopy()
    {
        var worktree = WriteConfig("worktree", """{ "setup-worktree": ["from worktree"] }""");
        var project = WriteConfig("project", """{ "setup-worktree": ["from project"] }""");

        CursorWorktreesConfig.Find(worktree, project).ShouldNotBeNull().Generic.ShouldNotBeNull().Commands.ShouldBe(["from worktree"]);
    }

    [Fact]
    public void FindFallsBackToTheProjectRootAndReturnsNullWhenNeitherHasAFile()
    {
        var worktree = Path.Combine(_directory, "worktree");
        Directory.CreateDirectory(worktree);
        var project = WriteConfig("project", """{ "setup-worktree": ["from project"] }""");
        var empty = Path.Combine(_directory, "empty");
        Directory.CreateDirectory(empty);

        CursorWorktreesConfig.Find(worktree, project).ShouldNotBeNull().Generic.ShouldNotBeNull().Commands.ShouldBe(["from project"]);
        CursorWorktreesConfig.Find(worktree, empty).ShouldBeNull();
    }

    [Fact]
    public void AMalformedFileInTheWorktreeIsAnErrorEvenWhenTheProjectHasAGoodOne()
    {
        var worktree = WriteConfig("worktree", "{ not json");
        var project = WriteConfig("project", """{ "setup-worktree": ["ok"] }""");

        Should.Throw<InvalidDataException>(() => CursorWorktreesConfig.Find(worktree, project));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{ "setup-worktree": 5 }""")]
    [InlineData("""{ "setup-worktree": ["ok", 7] }""")]
    [InlineData("""{ "setup-worktree": ["ok", "  "] }""")]
    [InlineData("""{ "setup-worktree-unix": "" }""")]
    [InlineData("""{ "setup-worktree-unix": "/etc/passwd" }""")]
    [InlineData("""{ "setup-worktree-unix": "../../outside.sh" }""")]
    public void MalformedFilesAreRejectedWithTheFilePathInTheMessage(string json)
    {
        var exception = Should.Throw<InvalidDataException>(() => CursorWorktreesConfig.Parse(json, FilePath));

        exception.Message.ShouldContain("worktrees.json", Case.Sensitive);
    }

    [Fact]
    public void ANullEntryMeansNoEntry()
    {
        CursorWorktreesConfig.Parse("""{ "setup-worktree": null }""", FilePath).Generic.ShouldBeNull();
    }
}
