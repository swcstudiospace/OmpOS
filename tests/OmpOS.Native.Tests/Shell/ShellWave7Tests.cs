using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using OmpOS.Harness.Completion;
using OmpOS.Native.Engine;
using OmpOS.Native.Views.Completion;
using OmpOS.Native.Views.Diff;
using OmpOS.Native.Views.Editor;
using OmpOS.Native.Views.Explorer;
using OmpOS.Native.Views.Export;
using OmpOS.Native.Views.InlineEditing;
using OmpOS.Native.Views.Mcp;
using OmpOS.Native.Views.Search;
using OmpOS.Native.Views.Terminal;
using OmpOS.Native.Views.Workspace;
using OmpOS.Presentation;
using OmpOS.Presentation.Approvals;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Shell;

/// <summary>
/// The workspace tools, the inline suggestions box and the launch path as the shell composes them. Every entry point opens its view,
/// the suggestions box sends nothing without a provider, and the application launches over a lazily composed engine without
/// starting a process.
/// </summary>
public sealed class ShellWave7Tests
{
    private const string NotesText = "first line\nsecond line\n";

    private static readonly string[] ToolToggleNames =
    [
        "EditorToggle",
        "ExplorerToggle",
        "TerminalToggle",
        "DiffToggle",
        "InlineEditToggle",
        "ConventionsToggle",
        "McpToggle",
        "WorkspaceSearchToggle",
        "ExportToggle",
    ];

    /// <summary>Each workspace tool's toggle is named and, when activated from the keyboard, shows its view in place of the transcript.</summary>
    [Theory]
    [InlineData("EditorToggle", typeof(EditorView))]
    [InlineData("ExplorerToggle", typeof(WorkspaceTreeView))]
    [InlineData("TerminalToggle", typeof(TerminalPanelView))]
    [InlineData("DiffToggle", typeof(DiffReviewView))]
    [InlineData("InlineEditToggle", typeof(InlineEditView))]
    [InlineData("ConventionsToggle", typeof(WorkspaceConventionsView))]
    [InlineData("McpToggle", typeof(McpToolsView))]
    [InlineData("WorkspaceSearchToggle", typeof(WorkspaceSearchView))]
    [InlineData("ExportToggle", typeof(TranscriptExportView))]
    public async Task EveryWorkspaceToolEntryPointOpensItsView(string toggleName, Type viewType)
    {
        using var workspace = new TempWorkspace();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShowWindow(workspace.Root);
            try
            {
                var toggle = Toggle(window, toggleName);
                AutomationProperties.GetName(toggle).ShouldNotBeNullOrWhiteSpace();

                Activate(window, toggle);

                toggle.IsChecked.ShouldBe(true);
                ToolHost(window).IsVisible.ShouldBeTrue();
                ToolHost(window).Content.ShouldBeOfType(viewType);
                TranscriptHost(window).IsVisible.ShouldBeFalse("the tool stands in for the transcript");
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Tab moves from the side-chat toggle through every workspace tool toggle, in the order the shell lists them.</summary>
    [Fact]
    public async Task WorkspaceToolTogglesFollowSideChatsInTabOrder()
    {
        using var workspace = new TempWorkspace();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShowWindow(workspace.Root);
            try
            {
                Toggle(window, "SideChatsToggle").Focus();
                foreach (var name in ToolToggleNames)
                {
                    ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
                    Toggle(window, name).IsFocused.ShouldBeTrue($"{name} follows the toggle before it");
                }
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Only one main-area view is open: opening a tool closes the open tool and the Agents view, and Enter on the open tool closes it.</summary>
    [Fact]
    public async Task OpeningAToolClosesTheOtherMainAreaViews()
    {
        using var workspace = new TempWorkspace();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShowWindow(workspace.Root);
            try
            {
                Activate(window, Toggle(window, "EditorToggle"));
                ToolHost(window).Content.ShouldBeOfType<EditorView>();

                Activate(window, Toggle(window, "TerminalToggle"));
                Toggle(window, "EditorToggle").IsChecked.ShouldBe(false);
                ToolHost(window).Content.ShouldBeOfType<TerminalPanelView>();

                Activate(window, Toggle(window, "AgentsToggle"));
                Toggle(window, "TerminalToggle").IsChecked.ShouldBe(false);
                ToolHost(window).IsVisible.ShouldBeFalse("the Agents view stands in for the transcript");
                window.FindControl<ContentControl>("AgentsHost")!.IsVisible.ShouldBeTrue();

                Activate(window, Toggle(window, "ExplorerToggle"));
                Toggle(window, "AgentsToggle").IsChecked.ShouldBe(false);
                window.FindControl<ContentControl>("AgentsHost")!.IsVisible.ShouldBeFalse();
                ToolHost(window).Content.ShouldBeOfType<WorkspaceTreeView>();

                Activate(window, Toggle(window, "ExplorerToggle"));
                Toggle(window, "ExplorerToggle").IsChecked.ShouldBe(false);
                ToolHost(window).IsVisible.ShouldBeFalse();
                TranscriptHost(window).IsVisible.ShouldBeTrue("closing the tool shows the transcript again");
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Opening a file from the explorer opens it in the editor, with its text.</summary>
    [Fact]
    public async Task OpeningAFileFromTheExplorerOpensItInTheEditor()
    {
        using var workspace = new TempWorkspace();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShowWindow(workspace.Root);
            try
            {
                Activate(window, Toggle(window, "ExplorerToggle"));
                var explorer = ToolHost(window).Content.ShouldBeOfType<WorkspaceTreeView>();

                explorer.FindControl<ListBox>("TreeRows")!.Focus();
                ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
                ShellHarness.Settle(window);

                var editor = ToolHost(window).Content.ShouldBeOfType<EditorView>();
                var model = editor.DataContext.ShouldBeOfType<EditorViewModel>();
                model.SelectedTab.ShouldNotBeNull();
                model.SelectedTab.RelativePath.ShouldBe("notes.txt");
                model.SelectedTab.Document.Text.ShouldBe(NotesText);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>While a tool replaces the transcript, the skip link lands on the tool's first control.</summary>
    [Fact]
    public async Task TheSkipLinkLandsOnTheOpenToolInsteadOfTheTranscript()
    {
        using var workspace = new TempWorkspace();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShowWindow(workspace.Root);
            try
            {
                Activate(window, Toggle(window, "ExplorerToggle"));
                var explorer = ToolHost(window).Content.ShouldBeOfType<WorkspaceTreeView>();

                ShellHarness.SkipLink(window).Focus();
                ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);

                explorer.FindControl<ListBox>("TreeRows")!.IsFocused.ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The inline suggestions toggle in the composer shows the suggestions box, and hides it again.</summary>
    [Fact]
    public async Task TheInlineSuggestionsToggleShowsTheSuggestionsBoxInTheComposer()
    {
        using var workspace = new TempWorkspace();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShowWindow(workspace.Root);
            try
            {
                var toggle = SuggestionsToggle(window);
                var box = Suggestions(window);
                AutomationProperties.GetName(toggle).ShouldNotBeNullOrWhiteSpace();
                box.IsVisible.ShouldBeFalse();

                Activate(window, toggle);
                box.IsVisible.ShouldBeTrue();

                Activate(window, toggle);
                box.IsVisible.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With no provider configured, typing in the suggestions box sends no request, even after the debounce has passed.</summary>
    [Fact]
    public async Task TheInlineSuggestionsBoxSendsNoRequestWithNoProviderConfigured()
    {
        using var workspace = new TempWorkspace();
        using var handler = new RecordingHandler(HttpStatusCode.OK, "{\"suggestion\":\"unexpected\"}");
        using var http = new HttpClient(handler);
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShowWindow(workspace.Root);
            try
            {
                ShellHarness.Composer(window).Completion = new CompletionClient(CompletionSettings.None, http);
                var box = Suggestions(window);
                Activate(window, SuggestionsToggle(window));

                box.Input.Text = "var total = ";
                box.Input.CaretIndex = box.Input.Text.Length;
                await SettleForAsync(window, TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken).ConfigureAwait(true);

                handler.Sent.ShouldBe(0);
                box.Suggestion.ShouldBeEmpty();
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The same box does ask a configured provider, which shows that the empty result above is not an unwired box.</summary>
    [Fact]
    public async Task TheInlineSuggestionsBoxAsksAConfiguredProviderAndShowsItsSuggestion()
    {
        using var workspace = new TempWorkspace();
        using var handler = new RecordingHandler(HttpStatusCode.OK, "{\"suggestion\":\"42\"}");
        using var http = new HttpClient(handler);
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShowWindow(workspace.Root);
            try
            {
                var settings = CompletionSettings.ForEndpoint(new Uri("http://127.0.0.1:9/complete"));
                ShellHarness.Composer(window).Completion = new CompletionClient(settings, http);
                var box = Suggestions(window);
                Activate(window, SuggestionsToggle(window));

                box.Input.Text = "answer = ";
                box.Input.CaretIndex = box.Input.Text.Length;
                await WaitUntilAsync(window, () => box.Suggestion.Length > 0, TestContext.Current.CancellationToken).ConfigureAwait(true);

                handler.Sent.ShouldBe(1);
                box.Suggestion.ShouldBe("42");
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Opening every workspace tool starts no child process: a terminal runs a command only after its approval.</summary>
    [Fact]
    public async Task OpeningTheWorkspaceToolsStartsNoProcess()
    {
        using var workspace = new TempWorkspace();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShowWindow(workspace.Root);
            try
            {
                var before = ProcessIds();
                foreach (var name in ToolToggleNames)
                {
                    Activate(window, Toggle(window, name));
                }

                ProcessIds().Except(before).ShouldBeEmpty("no tool starts a process when it opens");
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Building the application composes no engine, so none exists before the first decision.</summary>
    [Fact]
    public void BuildingTheApplicationComposesNoEngine()
    {
        var composed = 0;

        var builder = Program.BuildAvaloniaApp(() =>
        {
            composed++;
            return EngineComposition.Compose([]);
        });

        builder.ShouldNotBeNull();
        composed.ShouldBe(0);
    }

    /// <summary>
    /// Launch builds the window over the lazily composed engine's bridge and starts no process. The engine is composed on the first
    /// decision, and composing it starts no process either.
    /// </summary>
    [Fact]
    public async Task LaunchOverALazyEngineBuildsTheWindowAndStartsNoProcess()
    {
        using var workspace = new TempWorkspace();
        var composed = 0;
        var engine = new Lazy<EngineComposition>(() =>
        {
            composed++;
            return EngineComposition.Compose([]);
        });
        var bridge = new App.ComposedApprovalBridge(engine);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var before = ProcessIds();
            var window = new MainWindow(bridge, new UnconnectedRunInput(), MotionPreference.System, workspace.Root);
            window.Show();
            ShellHarness.Settle(window);
            try
            {
                composed.ShouldBe(0, "the engine is composed on the first decision, not at launch");
                ProcessIds().Except(before).ShouldBeEmpty("launch starts no child process");
                window.RunControls.UnavailableDetail.ShouldBeNull("the composed bridge is connected");

                var outcome = await bridge.DecideAsync("shell-session", "no-pending-request", ToolApprovalDecision.Deny, TestContext.Current.CancellationToken).ConfigureAwait(true);

                outcome.ShouldBe(ToolApprovalOutcome.AlreadyDecided);
                composed.ShouldBe(1);
                ProcessIds().Except(before).ShouldBeEmpty("composing the engine starts no child process");
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The window built without an engine keeps the unconnected approval bridge, which reports why it cannot record decisions.</summary>
    [Fact]
    public async Task TheDefaultWindowKeepsTheUnconnectedApprovalBridge()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = new MainWindow(MotionPreference.System);
            window.Show();
            ShellHarness.Settle(window);
            try
            {
                window.RunControls.UnavailableDetail.ShouldNotBeNullOrWhiteSpace();
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static MainWindow ShowWindow(string workspaceRoot)
    {
        var window = new MainWindow(new UnconnectedToolApprovalBridge(), new UnconnectedRunInput(), MotionPreference.System, workspaceRoot);
        window.Show();
        ShellHarness.Settle(window);
        return window;
    }

    private static ToggleButton Toggle(MainWindow window, string name) => window.FindControl<ToggleButton>(name)!;

    private static ToggleButton SuggestionsToggle(MainWindow window) => window.FindControl<ToggleButton>("SuggestionsToggle")!;

    private static InlineCompletionBox Suggestions(MainWindow window) =>
        ShellHarness.Composer(window).FindControl<InlineCompletionBox>("Suggestions")!;

    private static ContentControl ToolHost(MainWindow window) => window.FindControl<ContentControl>("ToolHost")!;

    private static ContentControl TranscriptHost(MainWindow window) => window.FindControl<ContentControl>("TranscriptHost")!;

    /// <summary>Focuses a toggle and activates it from the keyboard, as a user does, then settles the layout.</summary>
    private static void Activate(MainWindow window, ToggleButton toggle)
    {
        toggle.Focus();
        ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
        ShellHarness.Settle(window);
    }

    /// <summary>
    /// The processes this test process has started. On Linux these are the children of the test process, read from <c>/proc</c>, so
    /// processes other work on the machine starts are not counted. Elsewhere the whole process table is listed.
    /// </summary>
    private static HashSet<int> ProcessIds() => OperatingSystem.IsLinux() ? ChildProcessIds() : AllProcessIds();

    private static HashSet<int> ChildProcessIds()
    {
        var self = Environment.ProcessId;
        var children = new HashSet<int>();
        foreach (var directory in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
            {
                continue;
            }

            try
            {
                // The command name is in parentheses and may hold spaces. The fields after its closing parenthesis are the state, then the parent id.
                var stat = File.ReadAllText(Path.Combine(directory, "stat"));
                var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
                if (int.Parse(fields[1], CultureInfo.InvariantCulture) == self)
                {
                    children.Add(pid);
                }
            }
            catch (IOException)
            {
                continue; // The process ended before its stat file could be read.
            }
            catch (UnauthorizedAccessException)
            {
                continue; // The process is not readable by this user.
            }
        }

        return children;
    }

    private static HashSet<int> AllProcessIds()
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                ids.Add(process.Id);
            }
        }

        return ids;
    }

    private static async Task SettleForAsync(MainWindow window, TimeSpan duration, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < duration)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(true);
            ShellHarness.Settle(window);
        }
    }

    private static async Task WaitUntilAsync(MainWindow window, Func<bool> condition, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 500 && !condition(); attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(true);
            ShellHarness.Settle(window);
        }
    }

    /// <summary>A throwaway workspace folder that holds one text file.</summary>
    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Directory.CreateTempSubdirectory("ompos-shell-").FullName;
            File.WriteAllText(Path.Combine(Root, "notes.txt"), NotesText);
        }

        public string Root { get; }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    /// <summary>Answers every request with one fixed response and counts the requests sent through it.</summary>
    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        private int _sent;

        /// <summary>Gets the number of requests sent through this handler.</summary>
        public int Sent => _sent;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sent);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
