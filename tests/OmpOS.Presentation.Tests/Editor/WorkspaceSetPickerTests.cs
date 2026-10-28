using System.ComponentModel;
using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>The workspace set picker model: rows, primary, order, import and export, and the findings shown per row.</summary>
public sealed class WorkspaceSetPickerTests
{
    private static WorkspaceSetPicker Picker(ScriptedPickerBackend? backend = null, string name = "Workspace") => new(backend ?? new ScriptedPickerBackend(), name);

    /// <summary>The first folder becomes the primary, names come from the last segment and repeated names get a numeric suffix.</summary>
    [Fact]
    public void AddFolder_NamesRowsAfterTheirFoldersAndMakesTheFirstThePrimary()
    {
        var picker = Picker();

        picker.AddFolder("/work/a/src");
        picker.AddFolder("/work/b/src");
        picker.AddFolder("/work/api/");

        picker.Rows.Select(row => row.DisplayName).ShouldBe(["src", "src-2", "api"]);
        picker.Rows.Select(row => row.Id).ShouldBe(["src", "src-2", "api"]);
        picker.Rows.Select(row => row.IsPrimary).ShouldBe([true, false, false]);
        picker.Snapshot().Select(root => root.Path).ShouldBe(["/work/a/src", "/work/b/src", "/work/api/"]);
        Should.Throw<ArgumentException>(() => picker.AddFolder("  "));
    }

    /// <summary>A folder added to an environment's set belongs to that environment.</summary>
    [Fact]
    public void AddFolder_UsesTheEnvironmentOfThePrimary()
    {
        var picker = Picker();
        picker.Load("remote", [new WorkspacePickerRoot("app", "/srv/app", "app", "ssh-dev", IsPrimary: true)]);

        picker.AddFolder("/srv/lib");

        picker.Rows[1].EnvironmentId.ShouldBe("ssh-dev");
    }

    /// <summary>An empty picker cannot start, and the backend is not asked to validate nothing.</summary>
    [Fact]
    public void CanStart_IsFalseWhenEmptyAndTrueForAValidSet()
    {
        var backend = new ScriptedPickerBackend();
        var picker = Picker(backend);

        picker.CanStart.ShouldBeFalse();
        picker.AddFolder("/work/a");
        picker.CanStart.ShouldBeTrue();
        picker.Remove("a").ShouldBeTrue();

        picker.CanStart.ShouldBeFalse();
        picker.Issues.ShouldBeEmpty();
        backend.ValidatedSets.Select(set => set.Count).ShouldBe([1]);
    }

    /// <summary>An error blocks the set and shows on the row it is about; a warning shows but does not block.</summary>
    [Fact]
    public void Findings_AreShownOnTheirRowAndOnlyErrorsBlock()
    {
        var picker = Picker();
        picker.AddFolder("/work/a");
        picker.AddFolder("/work/a");
        picker.AddFolder("/work/notes");

        picker.CanStart.ShouldBeFalse();
        picker.Rows[0].HasError.ShouldBeFalse();
        picker.Rows[1].HasError.ShouldBeTrue();
        picker.Rows[1].IssueText.ShouldContain("same folder", Case.Sensitive);
        picker.Rows[2].HasWarning.ShouldBeTrue();
        picker.Rows[2].HasError.ShouldBeFalse();
        picker.HasWarnings.ShouldBeTrue();

        picker.Remove("a-2").ShouldBeTrue();

        picker.CanStart.ShouldBeTrue();
        picker.Rows[1].HasWarning.ShouldBeTrue();
        picker.Issues.ShouldHaveSingleItem().IsError.ShouldBeFalse();
    }

    /// <summary>Removing the primary promotes the next folder, and an unknown identifier removes nothing.</summary>
    [Fact]
    public void Remove_PromotesTheNextFolderWhenThePrimaryGoes()
    {
        var picker = Picker();
        picker.AddFolder("/work/a");
        picker.AddFolder("/work/b");
        picker.AddFolder("/work/c");

        picker.Remove("a").ShouldBeTrue();
        picker.Remove("nope").ShouldBeFalse();

        picker.Rows.Select(row => row.Id).ShouldBe(["b", "c"]);
        picker.Rows.Select(row => row.IsPrimary).ShouldBe([true, false]);
    }

    /// <summary>Making a folder primary moves it to the front and keeps the others in order.</summary>
    [Fact]
    public void MakePrimary_MovesTheFolderToTheFront()
    {
        var picker = Picker();
        picker.AddFolder("/work/a");
        picker.AddFolder("/work/b");
        picker.AddFolder("/work/c");

        picker.MakePrimary("c").ShouldBeTrue();
        picker.MakePrimary("nope").ShouldBeFalse();

        picker.Rows.Select(row => row.Id).ShouldBe(["c", "a", "b"]);
        picker.Rows.Select(row => row.IsPrimary).ShouldBe([true, false, false]);
    }

    /// <summary>Folders reorder among the non-primary rows, clamped, and the primary never moves.</summary>
    [Fact]
    public void Move_ReordersNonPrimaryFoldersAndKeepsThePrimaryFirst()
    {
        var picker = Picker();
        foreach (var name in new[] { "a", "b", "c", "d" })
        {
            picker.AddFolder("/work/" + name);
        }

        picker.Move("d", -1).ShouldBeTrue();
        picker.Rows.Select(row => row.Id).ShouldBe(["a", "b", "d", "c"]);

        picker.Move("d", -10).ShouldBeTrue();
        picker.Rows.Select(row => row.Id).ShouldBe(["a", "d", "b", "c"]);

        picker.Move("d", -1).ShouldBeFalse();
        picker.Move("a", 1).ShouldBeFalse();
        picker.Move("nope", 1).ShouldBeFalse();
        picker.Rows.Select(row => row.Id).ShouldBe(["a", "d", "b", "c"]);
    }

    /// <summary>Renaming changes the shown name only; a blank name is refused.</summary>
    [Fact]
    public void Rename_ChangesTheDisplayNameAndKeepsTheIdentifier()
    {
        var picker = Picker();
        picker.AddFolder("/work/a");

        picker.Rename("a", "  Backend ").ShouldBeTrue();
        picker.Rename("nope", "x").ShouldBeFalse();

        picker.Rows[0].DisplayName.ShouldBe("Backend");
        picker.Rows[0].Id.ShouldBe("a");
        Should.Throw<ArgumentException>(() => picker.Rename("a", " "));
    }

    /// <summary>Importing replaces the folders, the name and the warnings, with the first folder primary.</summary>
    [Fact]
    public void ImportCodeWorkspace_ReplacesTheFoldersAndShowsTheWarnings()
    {
        var backend = new ScriptedPickerBackend
        {
            Import = new WorkspacePickerImport(
                "team",
                [new WorkspacePickerRoot("web", "/repos/web", "Web", null, IsPrimary: false), new WorkspacePickerRoot("api", "/repos/api", "api", null, IsPrimary: true)],
                ["Folder 3 is given as a uri and was skipped."]),
        };
        var picker = Picker(backend);
        picker.AddFolder("/work/old");

        picker.ImportCodeWorkspace("/repos/team.code-workspace").ShouldBeTrue();

        picker.Name.ShouldBe("team");
        picker.Rows.Select(row => row.Id).ShouldBe(["api", "web"]);
        picker.Rows[0].IsPrimary.ShouldBeTrue();
        picker.ImportWarnings.ShouldBe(["Folder 3 is given as a uri and was skipped."]);
        picker.LastError.ShouldBeNull();
        backend.ImportedPaths.ShouldBe(["/repos/team.code-workspace"]);
    }

    /// <summary>A file that cannot be imported leaves the folders alone and says why.</summary>
    [Fact]
    public void ImportCodeWorkspace_KeepsTheFoldersAndSetsTheErrorOnFailure()
    {
        var picker = Picker(new ScriptedPickerBackend { ImportFailure = new InvalidDataException("'x.code-workspace' has no folder with a path.") });
        picker.AddFolder("/work/a");

        picker.ImportCodeWorkspace("/work/x.code-workspace").ShouldBeFalse();

        picker.Rows.Select(row => row.Id).ShouldBe(["a"]);
        picker.LastError.ShouldNotBeNull().ShouldContain("/work/x.code-workspace", Case.Sensitive);
        picker.LastError.ShouldContain("no folder with a path", Case.Sensitive);
    }

    /// <summary>Exporting hands the backend the folders, primary first; a failure sets the error.</summary>
    [Fact]
    public void ExportCodeWorkspace_WritesThroughTheBackendAndReportsFailures()
    {
        var backend = new ScriptedPickerBackend();
        var picker = Picker(backend);
        picker.AddFolder("/work/a");
        picker.AddFolder("/work/b");
        picker.MakePrimary("b");

        picker.ExportCodeWorkspace("/work/out.code-workspace").ShouldBeTrue();

        backend.Exported.ShouldHaveSingleItem().Roots.Select(root => root.Id).ShouldBe(["b", "a"]);
        backend.Exported[0].Path.ShouldBe("/work/out.code-workspace");
        picker.LastError.ShouldBeNull();

        backend.ExportFailure = new InvalidOperationException("'b' lives in environment 'ssh-dev'.");

        picker.ExportCodeWorkspace("/work/out.code-workspace").ShouldBeFalse();
        picker.LastError.ShouldNotBeNull().ShouldContain("ssh-dev", Case.Sensitive);
    }

    /// <summary>Loading a remembered set moves its primary to the front and refuses two primaries or repeated identifiers.</summary>
    [Fact]
    public void Load_NormalisesThePrimaryAndRefusesBadInput()
    {
        var picker = Picker();

        picker.Load("recent", [new WorkspacePickerRoot("a", "/a", "a", null, false), new WorkspacePickerRoot("b", "/b", "b", null, true)]);

        picker.Name.ShouldBe("recent");
        picker.Rows.Select(row => row.Id).ShouldBe(["b", "a"]);
        Should.Throw<ArgumentException>(() => picker.Load("x", [new WorkspacePickerRoot("a", "/a", "a", null, true), new WorkspacePickerRoot("b", "/b", "b", null, true)]));
        Should.Throw<ArgumentException>(() => picker.Load("x", [new WorkspacePickerRoot("a", "/a", "a", null, true), new WorkspacePickerRoot("a", "/b", "b", null, false)]));
        picker.Rows.Select(row => row.Id).ShouldBe(["b", "a"]);
    }

    /// <summary>Every change announces the state the view binds to, and a blank name is ignored.</summary>
    [Fact]
    public void Changes_RaisePropertyChangedForTheBoundState()
    {
        var picker = Picker();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)picker).PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        picker.AddFolder("/work/a");
        picker.Name = "renamed";
        picker.Name = "   ";

        raised.ShouldContain(nameof(WorkspaceSetPicker.CanStart), StringComparer.Ordinal);
        raised.ShouldContain(nameof(WorkspaceSetPicker.Issues), StringComparer.Ordinal);
        raised.ShouldContain(nameof(WorkspaceSetPicker.Name), StringComparer.Ordinal);
        picker.Name.ShouldBe("renamed");
    }

    private sealed record ExportCall(string Path, IReadOnlyList<WorkspacePickerRoot> Roots);

    private sealed class ScriptedPickerBackend : IWorkspaceSetPickerBackend
    {
        public List<IReadOnlyList<WorkspacePickerRoot>> ValidatedSets { get; } = [];

        public List<string> ImportedPaths { get; } = [];

        public List<ExportCall> Exported { get; } = [];

        public WorkspacePickerImport? Import { get; init; }

        public Exception? ImportFailure { get; init; }

        public Exception? ExportFailure { get; set; }

        // Rules of the fake: a path that repeats an earlier one is an error on the later row; a path containing "notes" is a warning.
        public IReadOnlyList<WorkspacePickerIssue> Validate(IReadOnlyList<WorkspacePickerRoot> roots)
        {
            ValidatedSets.Add(roots);
            var issues = new List<WorkspacePickerIssue>();
            for (var index = 0; index < roots.Count; index++)
            {
                if (roots.Take(index).Any(earlier => string.Equals(earlier.Path, roots[index].Path, StringComparison.Ordinal)))
                {
                    issues.Add(new WorkspacePickerIssue(IsError: true, roots[index].Id, $"'{roots[index].DisplayName}' is the same folder as an earlier root."));
                }

                if (roots[index].Path.Contains("notes", StringComparison.Ordinal))
                {
                    issues.Add(new WorkspacePickerIssue(IsError: false, roots[index].Id, $"'{roots[index].DisplayName}' is not a git repository."));
                }
            }

            return issues;
        }

        public WorkspacePickerImport ImportCodeWorkspace(string filePath)
        {
            ImportedPaths.Add(filePath);
            return ImportFailure is not null ? throw ImportFailure : Import ?? throw new InvalidDataException("nothing scripted");
        }

        public void ExportCodeWorkspace(string filePath, IReadOnlyList<WorkspacePickerRoot> roots)
        {
            if (ExportFailure is not null)
            {
                throw ExportFailure;
            }

            Exported.Add(new ExportCall(filePath, roots));
        }
    }
}
