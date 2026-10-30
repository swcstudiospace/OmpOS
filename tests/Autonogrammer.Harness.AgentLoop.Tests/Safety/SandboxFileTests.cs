using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>sandbox.json: the read boundary and the read allowlist, which replace the Settings value, the project over the user and Autonogrammer over Cursor.</summary>
public sealed class SandboxFileTests
{
    [Fact]
    public void TheTwoReadKeysAreReadAndOtherCursorKeysAreIgnored()
    {
        using var rig = new SafetyRig();
        rig.WriteProject(".autonogrammer/sandbox.json", """
            { "readBoundary": "Workspace", "additionalReadPaths": ["/opt/shared", "~/notes/*.md", 5],
              "type": "workspace_readwrite", "networkPolicy": { "default": "deny" } }
            """);

        var sandbox = rig.Sandbox();

        sandbox.ReadBoundary.ShouldBe(SandboxReadBoundary.Workspace);
        sandbox.ReadBoundarySource.ShouldBe(rig.InProject(".autonogrammer/sandbox.json"));
        sandbox.AdditionalReadPaths.ShouldBe(["/opt/shared", "~/notes/*.md"]);
        sandbox.AdditionalReadPathsSource.ShouldBe(rig.InProject(".autonogrammer/sandbox.json"));
        sandbox.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void WithNoFileTheSettingsValueApplies()
    {
        using var rig = new SafetyRig();

        var sandbox = SandboxLoader.Load(rig.Home, rig.Project, new SandboxSettings(SandboxReadBoundary.Workspace, ["/from/settings"]));

        sandbox.ReadBoundary.ShouldBe(SandboxReadBoundary.Workspace);
        sandbox.ReadBoundarySource.ShouldBeNull();
        sandbox.AdditionalReadPaths.ShouldBe(["/from/settings"]);
        sandbox.AdditionalReadPathsSource.ShouldBeNull();
    }

    [Fact]
    public void AFileValueReplacesTheSettingsValueAndTheProjectBeatsTheUserAndAutonogrammerBeatsCursor()
    {
        using var rig = new SafetyRig();
        rig.WriteHome(".cursor/sandbox.json", """{ "readBoundary": "workspace", "additionalReadPaths": ["/a"] }""");
        rig.WriteHome(".autonogrammer/sandbox.json", """{ "additionalReadPaths": ["/b"] }""");
        rig.WriteProject(".cursor/sandbox.json", """{ "readBoundary": "system" }""");

        var sandbox = SandboxLoader.Load(rig.Home, rig.Project, new SandboxSettings(SandboxReadBoundary.Workspace, ["/settings"]));

        sandbox.ReadBoundary.ShouldBe(SandboxReadBoundary.System, "the project's Cursor file wins over the user's");
        sandbox.ReadBoundarySource.ShouldBe(rig.InProject(".cursor/sandbox.json"));
        sandbox.AdditionalReadPaths.ShouldBe(["/b"]); // the user's Autonogrammer file wins over the user's Cursor file, and the list is replaced, not joined
        sandbox.AdditionalReadPathsSource.ShouldBe(rig.InHome(".autonogrammer/sandbox.json"));
    }

    [Fact]
    public void AnInvalidValueIsReportedAndIgnored()
    {
        using var rig = new SafetyRig();
        rig.WriteProject(".autonogrammer/sandbox.json", """{ "readBoundary": "everywhere", "additionalReadPaths": "x" }""");

        var sandbox = rig.Sandbox();

        sandbox.ReadBoundary.ShouldBe(SandboxReadBoundary.System);
        sandbox.ReadBoundarySource.ShouldBeNull();
        sandbox.Diagnostics.Count.ShouldBe(2);
        sandbox.Diagnostics.ShouldAllBe(problem => problem.Severity == SafetyDiagnosticSeverity.Warning);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    public void AFileThatCannotBeUsedIsIgnoredWithAnErrorAndNothingThrows(string text)
    {
        using var rig = new SafetyRig();
        rig.WriteProject(".autonogrammer/sandbox.json", text);

        var sandbox = rig.Sandbox();

        sandbox.ReadBoundary.ShouldBe(SandboxReadBoundary.System);
        sandbox.Diagnostics.ShouldHaveSingleItem().Severity.ShouldBe(SafetyDiagnosticSeverity.Error);
        sandbox.Files.Single(file => file.Exists).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AnUntrustedProjectCanOnlyTightenTheBoundary()
    {
        using var rig = new SafetyRig();
        rig.WriteHome(".autonogrammer/sandbox.json", """{ "readBoundary": "workspace", "additionalReadPaths": ["/user"] }""");
        rig.WriteProject(".autonogrammer/sandbox.json", """{ "readBoundary": "system", "additionalReadPaths": ["~/.ssh"] }""");

        var loosened = rig.Sandbox(trust: false);

        loosened.ReadBoundary.ShouldBe(SandboxReadBoundary.Workspace);
        loosened.AdditionalReadPaths.ShouldBe(["/user"]);
        loosened.Diagnostics.Count.ShouldBe(2);

        rig.WriteHome(".autonogrammer/sandbox.json", """{ "readBoundary": "system" }""");
        rig.WriteProject(".autonogrammer/sandbox.json", """{ "readBoundary": "workspace" }""");

        rig.Sandbox(trust: false).ReadBoundary.ShouldBe(SandboxReadBoundary.Workspace);
    }
}
