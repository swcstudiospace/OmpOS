using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>Legacy safety files stay enforced when alone, and are skipped with a named conflict when the current file exists.</summary>
public sealed class LegacySafetyDiscoveryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("legacy-safety-").FullName;

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>An existing legacy file sits between Cursor and the current file at user and project scope.</summary>
    [Fact]
    public void OrderIsCursorThenLegacyThenCurrentWhenTheLegacyFileExists()
    {
        Write(LegacyBrandNames.ConventionDirectory + "/permissions.json", Allow("Shell(ls)"));
        Write(".cursor/permissions.json", Allow("Shell(pwd)"));
        Write(".autonogrammer/permissions.json", Allow("Shell(echo)"));
        Write(LegacyBrandNames.ConventionDirectory + "/permissions.json", Allow("Shell(ls)"));
        var home = Path.Combine(_root, "home");
        Directory.CreateDirectory(Path.Combine(home, LegacyBrandNames.ConventionDirectory));
        Directory.CreateDirectory(Path.Combine(home, ".cursor"));
        Directory.CreateDirectory(Path.Combine(home, ".autonogrammer"));
        File.Copy(Path.Combine(_root, LegacyBrandNames.ConventionDirectory, "permissions.json"), Path.Combine(home, LegacyBrandNames.ConventionDirectory, "permissions.json"));
        File.WriteAllText(Path.Combine(home, ".cursor", "permissions.json"), Allow("Shell(pwd)"));
        File.WriteAllText(Path.Combine(home, ".autonogrammer", "permissions.json"), Allow("Shell(echo)"));
        var locations = SafetyFileLocations.Permissions(home, _root);
        var folders = locations.Where(location => location.Scope == PermissionScope.Project).Select(Folder).ToArray();
        folders.ShouldBe([".cursor", LegacyBrandNames.ConventionDirectory, ".autonogrammer"]);
        SafetyFileLocations.Sandbox(null, _root).ShouldNotBeEmpty();
    }

    /// <summary>A legacy-only permissions file and sandbox file are enforced and named as a conflict.</summary>
    [Fact]
    public void LegacyOnlySafetyFilesAreEnforcedAndReported()
    {
        Write(LegacyBrandNames.ConventionDirectory + "/permissions.json", Allow("Shell(ls)"));
        Write(LegacyBrandNames.ConventionDirectory + "/sandbox.json", """{ "readBoundary": "workspace", "additionalReadPaths": ["notes"] }""");

        var permissions = PermissionsLoader.LoadWithNotices(null, _root);
        permissions.Notices.ShouldHaveSingleItem().Enforced.ShouldBeTrue();
        permissions.Permissions.AllowRules.ShouldContain(rule => rule.Token.Text.Contains("ls", StringComparison.Ordinal));

        var sandbox = SandboxLoader.LoadWithNotices(null, _root);
        sandbox.Notices.ShouldHaveSingleItem().Path.ShouldContain(LegacyBrandNames.ConventionDirectory);
        sandbox.Sandbox.ReadBoundarySource.ShouldNotBeNull().ShouldContain(LegacyBrandNames.ConventionDirectory);
    }

    /// <summary>When both files exist the current file wins and the legacy file is named, not merged.</summary>
    [Fact]
    public void BothPresentSkipsLegacyRulesAndNamesTheFile()
    {
        Write(".autonogrammer/permissions.json", Allow("Shell(echo)"));
        Write(LegacyBrandNames.ConventionDirectory + "/permissions.json", Allow("Shell(rm)"));

        var loaded = PermissionsLoader.LoadWithNotices(null, _root);
        loaded.Permissions.AllowRules.ShouldContain(rule => rule.Token.Text.Contains("echo", StringComparison.Ordinal));
        loaded.Permissions.AllowRules.ShouldNotContain(rule => rule.Token.Text.Contains("rm", StringComparison.Ordinal));
        loaded.Notices.ShouldHaveSingleItem().Enforced.ShouldBeFalse();
        loaded.Notices[0].Message.ShouldContain("not merged", Case.Sensitive);
    }

    /// <summary>Run mode is read from the legacy file when the current file is absent, and save writes only the current path.</summary>
    [Fact]
    public async Task RunModeReadsLegacyAndSavesTheCurrentPath()
    {
        var ct = TestContext.Current.CancellationToken;
        var home = Path.Combine(_root, "home");
        Directory.CreateDirectory(Path.Combine(home, LegacyBrandNames.ConventionDirectory));
        await File.WriteAllTextAsync(Path.Combine(home, LegacyBrandNames.ConventionDirectory, "run-mode.json"), """{ "mode": "auto-review" }""", ct).ConfigureAwait(true);
        var before = await File.ReadAllTextAsync(Path.Combine(home, LegacyBrandNames.ConventionDirectory, "run-mode.json"), ct).ConfigureAwait(true);
        var store = new FileRunModeStore(home);

        var mode = await store.LoadAsync(ct).ConfigureAwait(true);
        mode.ShouldBe(RunMode.AutoReview);
        store.Notices.ShouldHaveSingleItem().Enforced.ShouldBeTrue();
        (await store.SaveAsync(RunMode.Allowlist, ct).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        File.Exists(Path.Combine(home, ".autonogrammer", "run-mode.json")).ShouldBeTrue();
        (await File.ReadAllTextAsync(Path.Combine(home, LegacyBrandNames.ConventionDirectory, "run-mode.json"), ct).ConfigureAwait(true)).ShouldBe(before);
    }

    /// <summary>Each safety file kind covers new only, legacy only, both, and neither.</summary>
    [Theory]
    [InlineData("permissions.json")]
    [InlineData("sandbox.json")]
    public void MatrixCoversTheFourLayouts(string fileName)
    {
        var project = Path.Combine(_root, fileName.Replace(".json", "", StringComparison.Ordinal));
        Directory.CreateDirectory(project);
        var listed = string.Equals(fileName, "sandbox.json", StringComparison.Ordinal) ? SafetyFileLocations.Sandbox(null, project) : SafetyFileLocations.Permissions(null, project);
        listed.Select(Folder).ShouldBe([".cursor", ".autonogrammer"]);
        Directory.CreateDirectory(Path.Combine(project, LegacyBrandNames.ConventionDirectory));
        File.WriteAllText(Path.Combine(project, LegacyBrandNames.ConventionDirectory, fileName), "{}");
        listed = string.Equals(fileName, "sandbox.json", StringComparison.Ordinal) ? SafetyFileLocations.Sandbox(null, project) : SafetyFileLocations.Permissions(null, project);
        listed.Select(Folder).ShouldBe([".cursor", LegacyBrandNames.ConventionDirectory, ".autonogrammer"]);
        Directory.CreateDirectory(Path.Combine(project, ".autonogrammer"));
        File.WriteAllText(Path.Combine(project, ".autonogrammer", fileName), "{}");
        listed = string.Equals(fileName, "sandbox.json", StringComparison.Ordinal) ? SafetyFileLocations.Sandbox(null, project) : SafetyFileLocations.Permissions(null, project);
        listed.Count(location => string.Equals(Folder(location), LegacyBrandNames.ConventionDirectory, StringComparison.Ordinal)).ShouldBe(1);
    }

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static string Allow(string token) => "{ \"permissions\": { \"allow\": [\"" + token + "\"] } }";

    private static string Folder(SafetyFileLocation location) => Path.GetFileName(Path.GetDirectoryName(location.Path))!;
}
