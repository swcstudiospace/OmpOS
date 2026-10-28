using OmpOS.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Safety;

/// <summary>Reading permissions.json: Cursor's keys, JSONC, the OmpOS token lists, and every way a file can be wrong without anything throwing.</summary>
public sealed class PermissionsFileTests
{
    private static PermissionsFileDocument ReadText(SafetyRig rig, string text)
    {
        var path = rig.WriteHome(".ompos/permissions.json", text);
        return PermissionsFileReader.Read(new SafetyFileLocation(path, PermissionScope.User, PermissionFileOrigin.Ompos));
    }

    [Fact]
    public void TheDocumentedKeysAreRead()
    {
        using var rig = new SafetyRig();

        var file = ReadText(rig, """
            {
              // comments and trailing commas are allowed
              "terminalAllowlist": ["git", "npm:install*",],
              "mcpAllowlist": ["github:*", "linear:list_issues"],
              "autoRun": { "allow_instructions": ["Reads under dist are fine."], "block_instructions": ["Never touch ~/.ssh."] },
              "permissions": { "allow": ["Read(src/**)"], "deny": ["Shell(rm)", "Write(**/*.key)"] },
              "unknownKey": 1
            }
            """);

        file.Exists.ShouldBeTrue();
        file.IsValid.ShouldBeTrue();
        file.TerminalAllowlist.ShouldBe(["git", "npm:install*"]);
        file.McpAllowlist.ShouldBe(["github:*", "linear:list_issues"]);
        file.AllowInstructions.ShouldBe(["Reads under dist are fine."]);
        file.BlockInstructions.ShouldBe(["Never touch ~/.ssh."]);
        file.AllowTokens.ShouldBe([new PermissionToken(PermissionTokenKind.Read, "src/**")]);
        file.DenyTokens.Select(token => token.Text).ToArray().ShouldBe(["Shell(rm)", "Write(**/*.key)"]);
        file.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void AMissingFileIsNotAProblem()
    {
        using var rig = new SafetyRig();

        var file = PermissionsFileReader.Read(new SafetyFileLocation(rig.InHome(".ompos/permissions.json"), PermissionScope.User, PermissionFileOrigin.Ompos));

        file.Exists.ShouldBeFalse();
        file.IsValid.ShouldBeTrue();
        file.TerminalAllowlist.ShouldBeNull();
        file.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void AKeyThatIsAbsentDiffersFromAnEmptyList()
    {
        using var rig = new SafetyRig();

        var file = ReadText(rig, """{ "mcpAllowlist": [] }""");

        file.McpAllowlist.ShouldNotBeNull().ShouldBeEmpty();
        file.TerminalAllowlist.ShouldBeNull();
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    [InlineData("\"text\"")]
    [InlineData("")]
    [InlineData("null")]
    public void AFileThatIsNotAJsonObjectIsIgnoredWithAnErrorAndNothingThrows(string text)
    {
        using var rig = new SafetyRig();

        var file = ReadText(rig, text);

        file.Exists.ShouldBeTrue();
        file.IsValid.ShouldBeFalse();
        file.TerminalAllowlist.ShouldBeNull();
        var problem = file.Diagnostics.ShouldHaveSingleItem();
        problem.Severity.ShouldBe(SafetyDiagnosticSeverity.Error);
        problem.File.ShouldBe(file.Location.Path);
        problem.Message.ShouldContain("ignored", Case.Sensitive);
    }

    [Fact]
    public void AFolderWhereTheFileShouldBeIsReportedNotRead()
    {
        using var rig = new SafetyRig();
        Directory.CreateDirectory(rig.InHome(".ompos/permissions.json"));

        var file = PermissionsFileReader.Read(new SafetyFileLocation(rig.InHome(".ompos/permissions.json"), PermissionScope.User, PermissionFileOrigin.Ompos));

        file.IsValid.ShouldBeFalse();
        file.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("folder", Case.Sensitive);
    }

    [Fact]
    public void AFileOverTheSizeLimitIsIgnored()
    {
        using var rig = new SafetyRig();

        var file = ReadText(rig, "{ \"terminalAllowlist\": [\"" + new string('a', SafetyJsonFile.MaxBytes) + "\"] }");

        file.IsValid.ShouldBeFalse();
        file.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("larger", Case.Sensitive);
    }

    [Fact]
    public void AByteOrderMarkIsAccepted()
    {
        using var rig = new SafetyRig();
        var path = rig.InHome(".ompos/permissions.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [.. new byte[] { 0xEF, 0xBB, 0xBF }, .. "{ \"terminalAllowlist\": [\"ls\"] }"u8.ToArray()]);

        var file = PermissionsFileReader.Read(new SafetyFileLocation(path, PermissionScope.User, PermissionFileOrigin.Ompos));

        file.IsValid.ShouldBeTrue();
        file.TerminalAllowlist.ShouldBe(["ls"]);
    }

    [Fact]
    public void EntriesOfTheWrongKindAreDroppedAsCursorDoes()
    {
        using var rig = new SafetyRig();

        var file = ReadText(rig, """{ "terminalAllowlist": ["git", 5, null, {"a":1}, "  ", " npm "] }""");

        file.TerminalAllowlist.ShouldBe(["git", "npm"]);
        file.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void AKeyOfTheWrongTypeIsReportedAndTheRestOfTheFileApplies()
    {
        using var rig = new SafetyRig();

        var file = ReadText(rig, """{ "terminalAllowlist": "git", "mcpAllowlist": ["a:b"], "autoRun": [], "permissions": 3 }""");

        file.IsValid.ShouldBeTrue();
        file.TerminalAllowlist.ShouldBeNull();
        file.McpAllowlist.ShouldBe(["a:b"]);
        file.Diagnostics.Select(problem => problem.Severity).Distinct().ShouldBe([SafetyDiagnosticSeverity.Warning]);
        file.Diagnostics.Count.ShouldBe(3);
    }

    [Fact]
    public void AnMcpEntryWithoutAColonAndAnInvalidTokenAreIgnoredWithAWarning()
    {
        using var rig = new SafetyRig();

        var file = ReadText(rig, """{ "mcpAllowlist": ["github", "ok:*"], "permissions": { "allow": ["Shell(git)", "Nope(x)", "Mcp(bad)"], "deny": "Shell(rm)" } }""");

        file.McpAllowlist.ShouldBe(["ok:*"]);
        file.AllowTokens.ShouldBe([new PermissionToken(PermissionTokenKind.Shell, "git")]);
        file.Diagnostics.Count.ShouldBe(4);
        file.Diagnostics.ShouldContain(problem => problem.Message.Contains("'github'", StringComparison.Ordinal));
        file.Diagnostics.ShouldContain(problem => problem.Message.Contains("Nope(x)", StringComparison.Ordinal));
    }

    [Fact]
    public void ALongEntryAndATooLongListAreCutWithAWarning()
    {
        using var rig = new SafetyRig();
        var many = string.Join(',', Enumerable.Range(0, SafetyJsonFile.MaxEntries + 5).Select(index => "\"c" + index + "\""));

        var file = ReadText(rig, "{ \"terminalAllowlist\": [" + many + ", \"" + new string('x', SafetyJsonFile.MaxEntryLength + 1) + "\"] }");

        file.TerminalAllowlist.ShouldNotBeNull().Count.ShouldBe(SafetyJsonFile.MaxEntries);
        file.Diagnostics.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TheEightFilesAreListedLowestPrecedenceFirst()
    {
        var locations = SafetyFileLocations.Permissions("/h", "/p");

        locations.Select(location => (location.Scope, location.Origin)).ToArray().ShouldBe(
        [
            (PermissionScope.User, PermissionFileOrigin.Cursor),
            (PermissionScope.User, PermissionFileOrigin.Ompos),
            (PermissionScope.Project, PermissionFileOrigin.Cursor),
            (PermissionScope.Project, PermissionFileOrigin.Ompos),
        ]);
        locations.Select(location => location.Path.Replace('\\', '/')).ToArray().ShouldBe(["/h/.cursor/permissions.json", "/h/.ompos/permissions.json", "/p/.cursor/permissions.json", "/p/.ompos/permissions.json"]);
        SafetyFileLocations.Sandbox(null, "/p").Select(location => location.Origin).ToArray().ShouldBe([PermissionFileOrigin.Cursor, PermissionFileOrigin.Ompos]);
        SafetyFileLocations.Permissions(null, null).ShouldBeEmpty();
    }
}
