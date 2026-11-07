using System.Text.Json;
using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>SetPermissionTokens writes permissions.allow and permissions.deny, keeps other keys, and refuses a path outside .autonogrammer.</summary>
public sealed class PermissionsFileEditorTokenTests
{
    [Fact]
    public void SetPermissionTokens_WritesAllowAndDeny_KeepsOtherKeys_AndLoaderReadsThemBack()
    {
        using var rig = new SafetyRig();
        var path = rig.InHome(".autonogrammer/permissions.json");
        SafetyRig.Write(path, """{ "terminalAllowlist": ["git"], "mcpAllowlist": ["kept:tool"] }""");

        PermissionsFileEditor.SetPermissionTokens(path, PermissionsFileEditor.AllowTokensList, ["Mcp(alpha:echo)"]).Succeeded.ShouldBeTrue();
        PermissionsFileEditor.SetPermissionTokens(path, PermissionsFileEditor.DenyTokensList, ["Mcp(alpha:rm)"]).Succeeded.ShouldBeTrue();

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        document.RootElement.GetProperty("terminalAllowlist")[0].GetString().ShouldBe("git");
        document.RootElement.GetProperty("mcpAllowlist")[0].GetString().ShouldBe("kept:tool");
        document.RootElement.GetProperty("permissions").GetProperty("allow")[0].GetString().ShouldBe("Mcp(alpha:echo)");
        document.RootElement.GetProperty("permissions").GetProperty("deny")[0].GetString().ShouldBe("Mcp(alpha:rm)");

        var loaded = PermissionsLoader.Load(rig.Home, projectRoot: null);
        loaded.AllowRules.Select(rule => rule.Token.Text).ShouldContain("Mcp(alpha:echo)", StringComparer.Ordinal);
        loaded.DenyRules.Select(rule => rule.Token.Text).ShouldContain("Mcp(alpha:rm)", StringComparer.Ordinal);
    }

    [Fact]
    public void SetPermissionTokens_RefusesAPathOutsideAutonogrammer()
    {
        using var rig = new SafetyRig();
        var path = rig.InHome("permissions.json");

        var result = PermissionsFileEditor.SetPermissionTokens(path, PermissionsFileEditor.AllowTokensList, ["Mcp(alpha:echo)"]);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain(".autonogrammer", Case.Sensitive);
        File.Exists(path).ShouldBeFalse();
    }
}
