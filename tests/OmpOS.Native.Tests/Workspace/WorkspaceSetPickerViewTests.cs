using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using OmpOS.Native.Tests.Settings;
using OmpOS.Native.Views.Workspace;
using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Workspace;

/// <summary>
/// The workspace set picker view and window on the headless desktop: every action reaches the model through the view, the folder and file
/// dialogs are the scripted seam, Start follows the rules, the keyboard works, every control is named and big enough, and Esc closes the window.
/// </summary>
public sealed class WorkspaceSetPickerViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WorkspacePickerRoot Root(string id, bool primary = false) => new(id, "/work/" + id, id, null, primary);

    /// <summary>The Add folder button asks the dialog seam, adds the answered folder as the primary row, and does nothing when the dialog is cancelled.</summary>
    [Fact]
    public async Task AddFolderAsksTheDialogAndShowsTheRow()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = new Rig();
            rig.View.EmptyMessage.IsVisible.ShouldBeTrue();
            rig.Dialogs.Folders.Enqueue("/work/api");
            rig.Dialogs.Folders.Enqueue(null);

            rig.Click(rig.View.AddFolder);

            rig.Picker.Rows.Select(row => row.Path).ShouldBe(["/work/api"]);
            rig.View.Folders.ItemCount.ShouldBe(1);
            rig.View.EmptyMessage.IsVisible.ShouldBeFalse();
            var shown = rig.Shown();
            shown.ShouldShowText("api");
            shown.ShouldShowText("/work/api");
            shown.ShouldShowText(WorkspaceSetPickerCopy.PrimaryLabel);

            rig.Click(rig.View.AddFolder);

            rig.Picker.Rows.Count.ShouldBe(1);
            rig.Dialogs.FolderRequests.ShouldBe(2);
            rig.Window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A row's Remove button removes it, and Delete on a focused row removes it; Delete in the name box removes nothing.</summary>
    [Fact]
    public async Task RemoveButtonAndDeleteKeyRemoveFolders()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = new Rig();
            rig.Picker.Load("set", [Root("a", primary: true), Root("b"), Root("c")]);
            ShellHarness.Settle(rig.Window);

            rig.Click(rig.ButtonNamed(WorkspaceSetPickerCopy.RemoveName("b")));

            rig.Picker.Rows.Select(row => row.Id).ShouldBe(["a", "c"]);
            rig.View.Folders.ItemCount.ShouldBe(2);

            _ = rig.View.NameInput.Focus();
            ViewHost.Press(rig.Window, Key.Delete, PhysicalKey.Delete);
            rig.Picker.Rows.Count.ShouldBe(2);

            rig.RowBorder("c").Focus().ShouldBeTrue();
            ViewHost.Press(rig.Window, Key.Delete, PhysicalKey.Delete);

            rig.Picker.Rows.Select(row => row.Id).ShouldBe(["a"]);
            rig.View.Folders.ItemCount.ShouldBe(1);
            rig.Window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Make primary moves a folder to the front; the primary row offers no Make primary button, and the others do.</summary>
    [Fact]
    public async Task MakePrimaryMovesTheFolderToTheFront()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = new Rig();
            rig.Picker.Load("set", [Root("a", primary: true), Root("b")]);
            ShellHarness.Settle(rig.Window);
            rig.VisibleButtons(WorkspaceSetPickerCopy.MakePrimaryName("a")).ShouldBeEmpty();

            rig.Click(rig.ButtonNamed(WorkspaceSetPickerCopy.MakePrimaryName("b")));

            rig.Picker.Rows.Select(row => row.Id).ShouldBe(["b", "a"]);
            rig.Picker.Rows.Select(row => row.IsPrimary).ShouldBe([true, false]);
            rig.VisibleButtons(WorkspaceSetPickerCopy.MakePrimaryName("b")).ShouldBeEmpty();
            rig.VisibleButtons(WorkspaceSetPickerCopy.MakePrimaryName("a")).Count.ShouldBe(1);
            rig.Window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Import sends the chosen file to the backend, shows the folders, the name and the skipped entries in the live region, and a failure shows its sentence and keeps the folders.</summary>
    [Fact]
    public async Task ImportFillsTheRowsAndShowsWarningsAndFailures()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            const string warning = "Skipped 'ssh://box/app': a remote folder cannot be imported.";
            var backend = new ViewScriptedBackend
            {
                Import = new WorkspacePickerImport("imported", [Root("x"), Root("y", primary: true)], [warning]),
            };
            var rig = new Rig(backend);
            rig.Dialogs.ImportPath = null;

            rig.Click(rig.View.Import);

            backend.ImportedPaths.ShouldBeEmpty();

            rig.Dialogs.ImportPath = "/work/set.code-workspace";
            rig.Click(rig.View.Import);

            backend.ImportedPaths.ShouldBe(["/work/set.code-workspace"]);
            rig.Picker.Name.ShouldBe("imported");
            rig.View.NameInput.Text.ShouldBe("imported");
            rig.Picker.Rows.Select(row => row.Id).ShouldBe(["y", "x"]);
            rig.View.ImportWarnings.ItemCount.ShouldBe(1);
            var shown = rig.Shown();
            shown.ShouldShowText(warning);
            shown.ShouldShowText(WorkspaceSetPickerCopy.WarningPrefix);
            rig.View.LastErrorLine.IsVisible.ShouldBeFalse();

            backend.ImportFailure = new InvalidDataException("not a workspace file");
            rig.Click(rig.View.Import);

            rig.View.LastErrorLine.IsVisible.ShouldBeTrue();
            rig.View.LastErrorLine.Text.ShouldNotBeNull().ShouldContain("not a workspace file", Case.Sensitive);
            rig.Picker.Rows.Select(row => row.Id).ShouldBe(["y", "x"]);
            rig.Window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Export offers the set name as a file name with a safe character set, writes the folders to the chosen path, and shows the failure sentence when the backend refuses.</summary>
    [Fact]
    public async Task ExportWritesThroughTheBackendAndShowsFailures()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var backend = new ViewScriptedBackend();
            var rig = new Rig(backend);
            rig.Picker.Load("my/set", [Root("a", primary: true), Root("b")]);
            ShellHarness.Settle(rig.Window);
            rig.Dialogs.ExportPath = "/work/out.code-workspace";

            rig.Click(rig.View.Export);

            rig.Dialogs.ExportNames.ShouldBe(["my-set.code-workspace"]);
            var exported = backend.Exported.ShouldHaveSingleItem();
            exported.Path.ShouldBe("/work/out.code-workspace");
            exported.Roots.Select(root => root.Id).ShouldBe(["a", "b"]);
            rig.View.LastErrorLine.IsVisible.ShouldBeFalse();

            backend.ExportFailure = new InvalidOperationException("'b' lives in environment 'ssh-dev'.");
            rig.Click(rig.View.Export);

            rig.View.LastErrorLine.IsVisible.ShouldBeTrue();
            rig.View.LastErrorLine.Text.ShouldNotBeNull().ShouldContain("ssh-dev", Case.Sensitive);
            backend.Exported.Count.ShouldBe(1);

            rig.Dialogs.ExportPath = null;
            backend.ExportFailure = null;
            rig.Click(rig.View.Export);

            backend.Exported.Count.ShouldBe(1);
            rig.Window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Start is disabled while the set is empty or has an error and then does nothing; a warning does not block; Done raises the cancel event.</summary>
    [Fact]
    public async Task StartIsDisabledUntilTheSetIsValid()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = new Rig();
            var started = 0;
            var cancelled = 0;
            rig.View.StartRequested += (_, _) => started++;
            rig.View.CancelRequested += (_, _) => cancelled++;

            rig.View.Start.IsEnabled.ShouldBeFalse();
            rig.Shown().ShouldShowText(WorkspaceSetPickerCopy.CannotStartHint);

            rig.Picker.AddFolder("/work/a");
            ShellHarness.Settle(rig.Window);

            rig.View.Start.IsEnabled.ShouldBeTrue();
            rig.Shown().ShouldNotShowText(WorkspaceSetPickerCopy.CannotStartHint);
            rig.Click(rig.View.Start);
            started.ShouldBe(1);

            rig.Picker.AddFolder("/work/a");
            ShellHarness.Settle(rig.Window);

            rig.View.Start.IsEnabled.ShouldBeFalse();
            rig.View.Findings.ItemCount.ShouldBe(1);
            rig.Shown().ShouldShowText(WorkspaceSetPickerCopy.ErrorPrefix);
            rig.Click(rig.View.Start);
            started.ShouldBe(1);

            rig.Picker.Remove("a-2").ShouldBeTrue();
            rig.Picker.AddFolder("/work/notes");
            ShellHarness.Settle(rig.Window);

            rig.View.Start.IsEnabled.ShouldBeTrue();
            rig.View.Findings.ItemCount.ShouldBe(1);
            rig.Shown().ShouldShowText(WorkspaceSetPickerCopy.WarningPrefix);

            rig.Click(rig.View.Done);
            cancelled.ShouldBe(1);
            started.ShouldBe(1);
            rig.Window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>With the footer hidden, as in the Settings page, Start and Done are not shown.</summary>
    [Fact]
    public async Task HidingTheFooterHidesStartAndDone()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = new Rig();
            rig.View.ShowFooter.ShouldBeTrue();
            rig.View.Start.IsEffectivelyVisible.ShouldBeTrue();

            rig.View.ShowFooter = false;
            ShellHarness.Settle(rig.Window);

            rig.View.Start.IsEffectivelyVisible.ShouldBeFalse();
            rig.View.Done.IsEffectivelyVisible.ShouldBeFalse();
            rig.Window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Typing a blank name keeps the model's name and the box shows it again once it loses focus.</summary>
    [Fact]
    public async Task ABlankNameIsRestoredWhenTheBoxLosesFocus()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = new Rig();
            rig.View.NameInput.Text.ShouldBe("Workspace");

            _ = rig.View.NameInput.Focus();
            rig.View.NameInput.Text = "Renamed";
            rig.Picker.Name.ShouldBe("Renamed");
            rig.View.NameInput.Text = "   ";
            rig.Picker.Name.ShouldBe("Renamed");

            _ = rig.View.AddFolder.Focus();
            ShellHarness.Settle(rig.Window);

            rig.View.NameInput.Text.ShouldBe("Renamed");
            rig.Window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Tab walks the name box, each row and its buttons, then Add folder, Import, Export, Done and Start, in that order.</summary>
    [Fact]
    public async Task TabFollowsTheVisualOrder()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var rig = new Rig();
            rig.Picker.Load("set", [Root("api", primary: true)]);
            ShellHarness.Settle(rig.Window);
            _ = rig.View.NameInput.Focus();
            SettingsTree.FocusedName(rig.Window).ShouldBe(WorkspaceSetPickerCopy.NameLabel);

            var visited = new List<string?>();
            for (var step = 0; step < 7; step++)
            {
                SettingsTree.PressTab(rig.Window);
                visited.Add(SettingsTree.FocusedName(rig.Window));
            }

            visited.ShouldBe(
            [
                "api",
                WorkspaceSetPickerCopy.RemoveName("api"),
                WorkspaceSetPickerCopy.AddFolderLabel,
                WorkspaceSetPickerCopy.ImportLabel,
                WorkspaceSetPickerCopy.ExportLabel,
                WorkspaceSetPickerCopy.DoneLabel,
                WorkspaceSetPickerCopy.StartLabel,
            ]);
            rig.Window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Every keyboard stop has an accessible name, every visible button and the name box meet the minimum target, and the live regions are polite for findings and assertive for errors.</summary>
    [Fact]
    public async Task EveryControlIsNamedAndBigEnoughAndTheLiveRegionsAreSet()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var backend = new ViewScriptedBackend { ExportFailure = new InvalidOperationException("cannot export a remote folder") };
            var rig = new Rig(backend);
            rig.Picker.Load("set", [Root("a", primary: true), Root("b"), Root("notes")]);
            rig.Dialogs.ExportPath = "/work/out.code-workspace";
            rig.Click(rig.View.Export);

            ViewHost.UnnamedTabStops(rig.View).ShouldBeEmpty();
            var buttons = rig.View.GetVisualDescendants().OfType<Button>().Where(button => button.TemplatedParent is null && button.IsEffectivelyVisible).ToList();
            buttons.Count.ShouldBeGreaterThan(6);
            foreach (var button in buttons)
            {
                button.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
                button.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            }

            rig.View.NameInput.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            foreach (var border in rig.RowBorders())
            {
                border.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            }

            AutomationProperties.GetLiveSetting(rig.View.Findings).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetLiveSetting(rig.View.ImportWarnings).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetLiveSetting(rig.View.LastErrorLine).ShouldBe(AutomationLiveSetting.Assertive);
            AutomationProperties.GetName(rig.View.Folders).ShouldBe(WorkspaceSetPickerCopy.FolderListName);
            rig.View.LastErrorLine.IsVisible.ShouldBeTrue();
            rig.Window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Esc closes the window without starting; Done does the same; Start sets Started, raises the event once and closes.</summary>
    [Fact]
    public async Task TheWindowClosesOnEscDoneAndStart()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var backend = new ViewScriptedBackend();

            var byEscape = new WindowRig(backend);
            byEscape.Window.Title.ShouldBe(WorkspaceSetPickerCopy.WindowTitle);
            SettingsTree.Press(byEscape.Window, Key.Escape, PhysicalKey.Escape);
            byEscape.Closed.ShouldBe(1);
            byEscape.Starts.ShouldBe(0);
            byEscape.Window.Started.ShouldBeFalse();

            var byDone = new WindowRig(backend);
            ShellHarness.RaiseClick(byDone.Window.View.Done);
            SettingsTree.SettleSafe(byDone.Window);
            byDone.Closed.ShouldBe(1);
            byDone.Starts.ShouldBe(0);
            byDone.Window.Started.ShouldBeFalse();

            var byStart = new WindowRig(backend);
            byStart.Window.Picker.Rows.Count.ShouldBe(1);
            ShellHarness.RaiseClick(byStart.Window.View.Start);
            SettingsTree.SettleSafe(byStart.Window);
            byStart.Closed.ShouldBe(1);
            byStart.Starts.ShouldBe(1);
            byStart.Window.Started.ShouldBeTrue();
            byStart.Window.Picker.Snapshot().Select(root => root.Path).ShouldBe(["/work/a"]);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The window passes its dialogs to the view, so the Add folder button of the window asks them.</summary>
    [Fact]
    public async Task TheWindowUsesTheDialogsItWasGiven()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var dialogs = new ScriptedDialogs();
            dialogs.Folders.Enqueue("/work/extra");
            var picker = new WorkspaceSetPicker(new ViewScriptedBackend());
            var window = new WorkspaceSetPickerWindow(picker, dialogs);
            window.Show();
            ShellHarness.Settle(window);

            ShellHarness.RaiseClick(window.View.AddFolder);
            ShellHarness.Settle(window);

            picker.Rows.Select(row => row.Path).ShouldBe(["/work/extra"]);
            window.Picker.ShouldBeSameAs(picker);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    private sealed class WindowRig
    {
        public WindowRig(ViewScriptedBackend backend)
        {
            var picker = new WorkspaceSetPicker(backend);
            picker.AddFolder("/work/a");
            Window = new WorkspaceSetPickerWindow(picker, new ScriptedDialogs());
            Window.Closed += (_, _) => Closed++;
            Window.StartRequested += (_, _) => Starts++;
            Window.Show();
            ShellHarness.Settle(Window);
        }

        public WorkspaceSetPickerWindow Window { get; }

        public int Closed { get; private set; }

        public int Starts { get; private set; }
    }

    private sealed class Rig
    {
        public Rig(ViewScriptedBackend? backend = null)
        {
            Backend = backend ?? new ViewScriptedBackend();
            Picker = new WorkspaceSetPicker(Backend);
            Dialogs = new ScriptedDialogs();
            View = new WorkspaceSetPickerView { DataContext = Picker, Dialogs = Dialogs };
            Window = ViewHost.Show(View);
        }

        public ViewScriptedBackend Backend { get; }

        public WorkspaceSetPicker Picker { get; }

        public ScriptedDialogs Dialogs { get; }

        public WorkspaceSetPickerView View { get; }

        public Window Window { get; }

        public void Click(Button button)
        {
            ShellHarness.RaiseClick(button);
            ShellHarness.Settle(Window);
        }

        public Button ButtonNamed(string name) =>
            View.GetVisualDescendants().OfType<Button>().First(button => button.IsEffectivelyVisible && string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

        public IReadOnlyList<Button> VisibleButtons(string name) =>
            [.. View.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible && string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal))];

        public IReadOnlyList<Border> RowBorders() =>
            [.. View.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("folder-row"))];

        public Border RowBorder(string displayName) =>
            RowBorders().First(border => string.Equals(AutomationProperties.GetName(border), displayName, StringComparison.Ordinal));

        public IReadOnlyList<string> Shown() =>
            [.. View.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text)).Select(text => text.Text!)];
    }

    private sealed class ScriptedDialogs : IWorkspaceSetPickerDialogs
    {
        public Queue<string?> Folders { get; } = new();

        public int FolderRequests { get; private set; }

        public string? ImportPath { get; set; }

        public string? ExportPath { get; set; }

        public List<string> ExportNames { get; } = [];

        public Task<string?> PickFolderAsync(CancellationToken cancellationToken)
        {
            FolderRequests++;
            return Task.FromResult(Folders.Count == 0 ? null : Folders.Dequeue());
        }

        public Task<string?> PickImportFileAsync(CancellationToken cancellationToken) => Task.FromResult(ImportPath);

        public Task<string?> PickExportFileAsync(string suggestedName, CancellationToken cancellationToken)
        {
            ExportNames.Add(suggestedName);
            return Task.FromResult(ExportPath);
        }
    }

    private sealed record ExportCall(string Path, IReadOnlyList<WorkspacePickerRoot> Roots);

    private sealed class ViewScriptedBackend : IWorkspaceSetPickerBackend
    {
        public List<string> ImportedPaths { get; } = [];

        public List<ExportCall> Exported { get; } = [];

        public WorkspacePickerImport? Import { get; init; }

        public Exception? ImportFailure { get; set; }

        public Exception? ExportFailure { get; set; }

        // Rules of the fake: a path that repeats an earlier one is an error on the later row; a path containing "notes" is a warning.
        public IReadOnlyList<WorkspacePickerIssue> Validate(IReadOnlyList<WorkspacePickerRoot> roots)
        {
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
