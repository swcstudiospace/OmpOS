using System.Text.Json;
using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>Writing the Autonogrammer-owned files: only they can be written, nothing else in the file is lost, and a file that cannot be rewritten safely is left exactly as it was.</summary>
public sealed class AutonogrammerFilesTests
{
    [Fact]
    public void AnAllowlistIsWrittenToANewFileAndReadBackByTheReader()
    {
        using var rig = new SafetyRig();
        var path = rig.InProject(".autonogrammer/permissions.json");

        var result = PermissionsFileEditor.SetAllowlist(path, PermissionsFileEditor.TerminalAllowlistKey, ["git", "npm:install*"]);

        result.Succeeded.ShouldBeTrue();
        var document = PermissionsFileReader.Read(new SafetyFileLocation(path, PermissionScope.Project, PermissionFileOrigin.Autonogrammer));
        document.TerminalAllowlist.ShouldBe(["git", "npm:install*"]);
        document.McpAllowlist.ShouldBeNull();
        File.ReadAllText(path).ShouldEndWith("\n", Case.Sensitive);
        Directory.GetFiles(rig.InProject(".autonogrammer")).Select(Path.GetFileName).ToArray().ShouldBe(["permissions.json"]);
    }

    [Fact]
    public void OtherKeysAndTheOtherAllowlistAreKept()
    {
        using var rig = new SafetyRig();
        var path = rig.WriteProject(".autonogrammer/permissions.json", """{ "mcpAllowlist": ["a:*"], "autoRun": { "block_instructions": ["never"] }, "extra": [1, 2] }""");

        PermissionsFileEditor.SetAllowlist(path, PermissionsFileEditor.TerminalAllowlistKey, ["ls"]).Succeeded.ShouldBeTrue();

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        root.GetProperty("terminalAllowlist").EnumerateArray().Select(item => item.GetString()).ToArray().ShouldBe(["ls"]);
        root.GetProperty("mcpAllowlist").EnumerateArray().Single().GetString().ShouldBe("a:*");
        root.GetProperty("autoRun").GetProperty("block_instructions").EnumerateArray().Single().GetString().ShouldBe("never");
        root.GetProperty("extra").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public void AnEmptyListIsWrittenWhenTheKeyExistsAndLeftOutWhenItDoesNot()
    {
        using var rig = new SafetyRig();
        var path = rig.WriteProject(".autonogrammer/permissions.json", """{ "terminalAllowlist": ["git"] }""");

        PermissionsFileEditor.SetAllowlist(path, PermissionsFileEditor.TerminalAllowlistKey, []).Succeeded.ShouldBeTrue();
        PermissionsFileEditor.SetAllowlist(path, PermissionsFileEditor.McpAllowlistKey, []).Succeeded.ShouldBeTrue();

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        document.RootElement.GetProperty("terminalAllowlist").GetArrayLength().ShouldBe(0);
        document.RootElement.TryGetProperty("mcpAllowlist", out _).ShouldBeFalse();
    }

    [Fact]
    public void ASettingThatChangesNothingDoesNotTouchTheFile()
    {
        using var rig = new SafetyRig();
        var path = rig.InProject(".autonogrammer/permissions.json");
        PermissionsFileEditor.SetAllowlist(path, PermissionsFileEditor.TerminalAllowlistKey, ["git"]).Succeeded.ShouldBeTrue();
        var stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, stamp);

        PermissionsFileEditor.SetAllowlist(path, PermissionsFileEditor.TerminalAllowlistKey, ["git"]).Succeeded.ShouldBeTrue();

        File.GetLastWriteTimeUtc(path).ShouldBe(stamp);
    }

    [Theory]
    [InlineData(".cursor/permissions.json")]
    [InlineData(".autonogrammer/other.json")]
    [InlineData(".autonogrammer/sub/permissions.json")]
    [InlineData("permissions.json")]
    [InlineData(".autonogrammer2/permissions.json")]
    public void OnlyAnAutonogrammerFileOfTheRightNameCanBeWritten(string relative)
    {
        using var rig = new SafetyRig();
        var path = rig.InProject(relative);

        var result = PermissionsFileEditor.SetAllowlist(path, PermissionsFileEditor.TerminalAllowlistKey, ["git"]);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain(".autonogrammer", Case.Sensitive);
        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public void ARelativePathAndAnUnknownKeyAreRefused()
    {
        using var rig = new SafetyRig();

        PermissionsFileEditor.SetAllowlist(".autonogrammer/permissions.json", PermissionsFileEditor.TerminalAllowlistKey, ["git"]).Succeeded.ShouldBeFalse();
        PermissionsFileEditor.SetAllowlist(rig.InProject(".autonogrammer/permissions.json"), "autoRun", ["git"]).Succeeded.ShouldBeFalse();
    }

    [Theory]
    [InlineData("{ not json", "not valid JSON")]
    [InlineData("[1]", "one JSON object")]
    [InlineData("{ \"terminalAllowlist\": [\"a\"], // note\n }", "comments or trailing commas")]
    [InlineData("{ \"a\": 1, }", "comments or trailing commas")]
    public void AFileThatCannotBeRewrittenSafelyIsLeftExactlyAsItWas(string text, string reason)
    {
        using var rig = new SafetyRig();
        var path = rig.WriteProject(".autonogrammer/permissions.json", text);

        var result = PermissionsFileEditor.SetAllowlist(path, PermissionsFileEditor.TerminalAllowlistKey, ["git"]);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain(reason, Case.Sensitive);
        File.ReadAllText(path).ShouldBe(text);
        Directory.GetFiles(rig.InProject(".autonogrammer")).Length.ShouldBe(1);
    }

    [Fact]
    public void AFolderInPlaceOfTheFileIsReportedNotOverwritten()
    {
        using var rig = new SafetyRig();
        Directory.CreateDirectory(rig.InProject(".autonogrammer/permissions.json"));

        var result = PermissionsFileEditor.SetAllowlist(rig.InProject(".autonogrammer/permissions.json"), PermissionsFileEditor.McpAllowlistKey, ["a:b"]);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain("folder", Case.Sensitive);
    }

    [Fact]
    public void TheSandboxFileKeepsItsOtherKeysAndRoundTripsThroughTheReader()
    {
        using var rig = new SafetyRig();
        var path = rig.WriteProject(".autonogrammer/sandbox.json", """{ "type": "workspace_readwrite", "networkPolicy": { "default": "deny" } }""");

        SandboxFileEditor.Set(path, SandboxReadBoundary.Workspace, ["~/notes/*.md"]).Succeeded.ShouldBeTrue();

        var document = SandboxFileReader.Read(new SafetyFileLocation(path, PermissionScope.Project, PermissionFileOrigin.Autonogrammer));
        document.ReadBoundary.ShouldBe(SandboxReadBoundary.Workspace);
        document.AdditionalReadPaths.ShouldBe(["~/notes/*.md"]);
        File.ReadAllText(path).ShouldContain("networkPolicy", Case.Sensitive);
        SandboxFileEditor.Set(rig.InProject(".cursor/sandbox.json"), SandboxReadBoundary.System, []).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task TheRunModeIsKeptInTheAutonogrammerFolderAndAMissingOrBrokenStoreGivesTheDefault()
    {
        using var rig = new SafetyRig();
        var store = new FileRunModeStore(rig.Home);
        var ct = TestContext.Current.CancellationToken;

        (await store.LoadAsync(ct).ConfigureAwait(true)).ShouldBe(RunMode.AutoReview);
        (await store.SaveAsync(RunMode.RunEverything, ct).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await store.LoadAsync(ct).ConfigureAwait(true)).ShouldBe(RunMode.RunEverything);
        (await File.ReadAllTextAsync(rig.InHome(".autonogrammer/run-mode.json"), TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("\"unrestricted\"", Case.Sensitive);

        SafetyRig.Write(rig.InHome(".autonogrammer/run-mode.json"), "{ broken");
        (await store.LoadAsync(ct).ConfigureAwait(true)).ShouldBe(RunMode.AutoReview);
        SafetyRig.Write(rig.InHome(".autonogrammer/run-mode.json"), """{ "mode": "nonsense" }""");
        (await store.LoadAsync(ct).ConfigureAwait(true)).ShouldBe(RunMode.AutoReview);
        (await store.SaveAsync(RunMode.Allowlist, ct).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await store.LoadAsync(ct).ConfigureAwait(true)).ShouldBe(RunMode.Allowlist);
    }

    [Fact]
    public async Task ASaveThatCannotRewriteABrokenStoreReportsItAndKeepsTheFile()
    {
        using var rig = new SafetyRig();
        SafetyRig.Write(rig.InHome(".autonogrammer/run-mode.json"), "{ broken");
        var store = new FileRunModeStore(rig.Home);

        var result = await store.SaveAsync(RunMode.Allowlist, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        (await File.ReadAllTextAsync(rig.InHome(".autonogrammer/run-mode.json"), TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe("{ broken");
    }
}
