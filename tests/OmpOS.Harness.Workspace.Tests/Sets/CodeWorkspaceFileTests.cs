using System.Text.Json.Nodes;
using OmpOS.Workspace.Sets;
using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests.Sets;

/// <summary>
/// Verifies reading and writing <c>.code-workspace</c> files: comments and trailing commas, relative paths, skipped folders,
/// and that settings the file holds survive a round trip.
/// </summary>
public sealed class CodeWorkspaceFileTests : IDisposable
{
    private readonly TempWorkspace _directory = new();

    private string FilePath => Path.Combine(_directory.RootPath, "ws", "team.code-workspace");

    public void Dispose() => _directory.Dispose();

    private string Abs(string relative) => Path.GetFullPath(Path.Combine(_directory.RootPath, relative));

    [Fact]
    public void ReadsJsoncWithCommentsAndTrailingCommasAndResolvesRelativeFoldersAgainstTheFile()
    {
        const string text = """
            {
              // the two repositories
              "folders": [
                { "path": "../web", "name": "Web App" },
                { "path": "../api" }, /* block */
              ],
              "settings": { "editor.tabSize": 2, },
            }
            """;

        var import = CodeWorkspaceFile.Parse(text, FilePath).ToWorkspaceSet();

        import.Warnings.ShouldBeEmpty();
        import.Set.Name.ShouldBe("team");
        import.Set.Id.ShouldBe("team");
        import.Set.Roots.Select(root => root.Path).ShouldBe([Abs("web"), Abs("api")]);
        import.Set.Roots.Select(root => root.DisplayName).ShouldBe(["Web App", "api"]);
        import.Set.Roots.Select(root => root.Id).ShouldBe(["web-app", "api"]);
        import.Set.Primary.Path.ShouldBe(Abs("web"));
    }

    [Fact]
    public void AnAbsoluteFolderStaysAbsoluteAndADotMeansTheDirectoryOfTheFile()
    {
        var text = $$"""{ "folders": [ { "path": "." }, { "path": {{JsonValue.Create(Abs("elsewhere/lib"))!.ToJsonString()}} } ] }""";

        var set = CodeWorkspaceFile.Parse(text, FilePath).ToWorkspaceSet().Set;

        set.Roots.Select(root => root.Path).ShouldBe([Abs("ws"), Abs("elsewhere/lib")]);
    }

    [Fact]
    public void UriFoldersAndFoldersWithoutAPathAreSkippedWithAWarning()
    {
        const string text = """
            { "folders": [
              { "uri": "remote-host://ssh-remote+dev/srv/app" },
              { "path": "app" },
              { "name": "nothing" }
            ] }
            """;

        var import = CodeWorkspaceFile.Parse(text, FilePath).ToWorkspaceSet();

        import.Set.Roots.ShouldHaveSingleItem().Path.ShouldBe(Abs("ws/app"));
        import.Warnings.Count.ShouldBe(2);
        import.Warnings[0].ShouldContain("Folder 1", Case.Sensitive);
        import.Warnings[0].ShouldContain("uri", Case.Sensitive);
        import.Warnings[1].ShouldContain("Folder 3", Case.Sensitive);
    }

    [Theory]
    [InlineData("{ }")]
    [InlineData("""{ "folders": [] }""")]
    [InlineData("""{ "folders": [ { "uri": "x" } ] }""")]
    public void AFileWithoutAUsableFolderCannotBecomeASet(string text)
    {
        Should.Throw<InvalidDataException>(() => CodeWorkspaceFile.Parse(text, FilePath).ToWorkspaceSet());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "folders": "web" }""")]
    [InlineData("""{ "folders": [ "web" ] }""")]
    public void MalformedContentIsRejectedWithTheFileInTheMessage(string text)
    {
        var exception = Should.Throw<InvalidDataException>(() => CodeWorkspaceFile.Parse(text, FilePath));

        exception.Message.ShouldContain("team.code-workspace", Case.Sensitive);
    }

    [Fact]
    public void ExportingIntoTheFileItCameFromKeepsEveryOtherSettingAndFolderKey()
    {
        const string text = """
            {
              // comment is dropped, the data is kept
              "folders": [ { "path": "../web", "name": "Web", "x-owner": "team-a" }, { "uri": "remote-host://h/x" } ],
              "settings": { "editor.tabSize": 2, "files.exclude": { "**/obj": true } },
              "extensions": { "recommendations": [ "ms-dotnettools.csharp" ] },
              "launch": { "version": "0.2.0" }
            }
            """;
        var file = CodeWorkspaceFile.Parse(text, FilePath);
        var set = file.ToWorkspaceSet().Set.AddFolder(Abs("services/billing"));

        var json = JsonNode.Parse(file.WithSet(set).ToJson())!.AsObject();

        json["settings"]!["editor.tabSize"]!.GetValue<int>().ShouldBe(2);
        json["settings"]!["files.exclude"]!["**/obj"]!.GetValue<bool>().ShouldBeTrue();
        json["extensions"]!["recommendations"]![0]!.GetValue<string>().ShouldBe("ms-dotnettools.csharp");
        json["launch"]!["version"]!.GetValue<string>().ShouldBe("0.2.0");
        var folders = json["folders"]!.AsArray();
        folders.Count.ShouldBe(3);
        folders[0]!["path"]!.GetValue<string>().ShouldBe("../web");
        folders[0]!["name"]!.GetValue<string>().ShouldBe("Web");
        folders[0]!["x-owner"]!.GetValue<string>().ShouldBe("team-a");
        folders[1]!["path"]!.GetValue<string>().ShouldBe("../services/billing");
        folders[1]!["name"].ShouldBeNull();
        folders[2]!["uri"]!.GetValue<string>().ShouldBe("remote-host://h/x");
    }

    [Fact]
    public void RemovingAFolderFromTheSetRemovesItFromTheExportedFile()
    {
        var set = WorkspaceSet.FromPaths("team", [Abs("web"), Abs("api"), Abs("docs")]).Remove("api");

        var file = CodeWorkspaceFile.Create(set, FilePath);

        file.ToWorkspaceSet().Set.Roots.Select(root => root.Path).ShouldBe([Abs("web"), Abs("docs")]);
    }

    [Fact]
    public void ARoundTripThroughTheFileSystemKeepsPathsNamesAndThePrimary()
    {
        var set = WorkspaceSet.FromPaths("team", [Abs("repos/web"), Abs("repos/api"), Abs("repos/docs")])
            .WithPrimary("api")
            .Rename("team");
        var renamed = new WorkspaceSet(set.Id, set.Name, set.Roots.Select(root => string.Equals(root.Id, "docs", StringComparison.Ordinal) ? root with { DisplayName = "Documentation" } : root));

        CodeWorkspaceFile.Create(renamed, FilePath).Write();
        var back = CodeWorkspaceFile.Read(FilePath).ToWorkspaceSet().Set;

        back.Roots.Select(root => root.Path).ShouldBe([Abs("repos/api"), Abs("repos/web"), Abs("repos/docs")]);
        back.Roots.Select(root => root.DisplayName).ShouldBe(["api", "web", "Documentation"]);
        back.Primary.Id.ShouldBe("api");
    }

    [Fact]
    public void FoldersNearTheFileAreWrittenRelativeAndFarOnesAbsolute()
    {
        var far = Path.Combine(Path.GetPathRoot(_directory.RootPath)!, "ompos-far", "one", "two", "three", "repo");
        var set = WorkspaceSet.FromPaths("team", [Abs("ws/inner"), Abs("sibling"), far]);

        var folders = JsonNode.Parse(CodeWorkspaceFile.Create(set, FilePath).ToJson())!["folders"]!.AsArray();

        folders[0]!["path"]!.GetValue<string>().ShouldBe("inner");
        folders[1]!["path"]!.GetValue<string>().ShouldBe("../sibling");
        folders[2]!["path"]!.GetValue<string>().ShouldBe(far.Replace('\\', '/'));
    }

    [Fact]
    public void ARemoteRootCannotBeExported()
    {
        var set = WorkspaceSet.FromPaths("team", ["/srv/app"], environmentId: "ssh-dev");

        var exception = Should.Throw<InvalidOperationException>(() => CodeWorkspaceFile.Create(set, FilePath));

        exception.Message.ShouldContain("ssh-dev", Case.Sensitive);
    }

    [Fact]
    public void TheWrittenFileIsTabIndentedJsonWithoutAByteOrderMark()
    {
        var set = WorkspaceSet.FromPaths("team", [Abs("ws/app")]);

        CodeWorkspaceFile.Create(set, FilePath).Write();
        var bytes = File.ReadAllBytes(FilePath);

        bytes.Take(3).ShouldNotBe([0xEF, 0xBB, 0xBF]);
        var text = File.ReadAllText(FilePath);
        text.ShouldBe("{\n\t\"folders\": [\n\t\t{\n\t\t\t\"path\": \"app\"\n\t\t}\n\t]\n}\n");
    }
}
