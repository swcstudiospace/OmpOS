using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Native.Engine;
using OmpOS.Native.Views.Changes;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.State;

namespace OmpOS.Native.Tests.Changes;

/// <summary>
/// A shell over a real workspace folder and one session, in a shown window: a test feeds the session's transcript file-edit blocks the way a
/// run does and reads what the Changes bar and the Diff review tool show.
/// </summary>
internal sealed class ChangesFixture : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "ompos-changes-view-" + Guid.NewGuid().ToString("N"));
    private readonly AgentPanelViewModel _panel;
    private long _seq;

    /// <summary>Creates the workspace folder, the panel over it and the shown window.</summary>
    public ChangesFixture()
    {
        Root = Path.Combine(_sandbox, "root");
        Directory.CreateDirectory(Root);
        _panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), Root, MotionPreference.System);
        Main = new MainView(_panel, Root);
        Window = new Window { Content = Main, Width = 1280, Height = 800 };
        Window.Show();
        ShellHarness.Settle(Window);
    }

    /// <summary>Gets the absolute path of the workspace folder.</summary>
    public string Root { get; }

    /// <summary>Gets the main view the window shows.</summary>
    public MainView Main { get; }

    /// <summary>Gets the shown window.</summary>
    public Window Window { get; }

    /// <summary>Gets the transcript of the shown session.</summary>
    public SessionViewModel Session => _panel.Selected?.Session ?? throw new InvalidOperationException("No session is selected.");

    /// <summary>Gets the Changes bar the shell shows under the transcript.</summary>
    public ChangesBarView Bar => (ChangesBarView)Main.FindControl<ContentControl>("ChangesHost")!.Content!;

    /// <summary>Gets the host of the open workspace tool.</summary>
    public ContentControl ToolHost => Main.FindControl<ContentControl>("ToolHost")!;

    /// <summary>Writes a file in the workspace, and its folders.</summary>
    /// <param name="relativePath">The workspace-relative path.</param>
    /// <param name="text">The text.</param>
    public void Write(string relativePath, string text)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>Reads a file of the workspace.</summary>
    /// <param name="relativePath">The workspace-relative path.</param>
    /// <returns>The text.</returns>
    public string Read(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Tells whether a file exists in the workspace.</summary>
    /// <param name="relativePath">The workspace-relative path.</param>
    /// <returns>True when it does.</returns>
    public bool Exists(string relativePath) => File.Exists(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Feeds the transcript a completed tool call that edited a file, as a run does, and lets the views catch up.</summary>
    /// <param name="callId">The tool call identifier.</param>
    /// <param name="path">The path the agent reported.</param>
    /// <param name="oldText">The text before the edit, or null for a created file.</param>
    /// <param name="newText">The text after the edit.</param>
    /// <param name="status">The call status.</param>
    public void Edit(string callId, string path, string? oldText, string newText, string status = "completed")
    {
        using var arguments = JsonDocument.Parse("{}");
        Feed(new ToolCallBlock { Id = callId, CallId = callId, ToolName = "edit", Status = status, Arguments = arguments.RootElement.Clone() });
        Feed(new FileEditBlock { Id = callId + "-edit-0", CallId = callId, Path = path, OldText = oldText, NewText = newText });
        ShellHarness.Settle(Window);
    }

    /// <summary>Gets the button with the given accessible name in a control's tree.</summary>
    /// <param name="root">The control to search.</param>
    /// <param name="name">The accessible name.</param>
    /// <returns>The button.</returns>
    public static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

    /// <summary>Gets the texts of every text block in a control's tree.</summary>
    /// <param name="root">The control to search.</param>
    /// <returns>The texts.</returns>
    public static IReadOnlyList<string> Texts(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(static block => block.Text ?? string.Empty).ToList();

    /// <summary>Gets the text block of a control's tree that shows exactly the given text.</summary>
    /// <param name="root">The control to search.</param>
    /// <param name="text">The text.</param>
    /// <returns>The text block.</returns>
    public static TextBlock TextBlockWith(Control root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().First(block => string.Equals(block.Text, text, StringComparison.Ordinal));

    /// <summary>Closes the window, releases the view and removes the workspace.</summary>
    public void Dispose()
    {
        Window.Close();
        Main.Close();
        _panel.Dispose();
        if (Directory.Exists(_sandbox))
        {
            Directory.Delete(_sandbox, recursive: true);
        }
    }

    private void Feed(UiBlock block)
    {
        _seq++;
        Session.ApplyEvent(new StreamEvent
        {
            Seq = _seq,
            SessionId = UnconnectedAgentPanel.SessionId,
            CorrelationId = "turn-1",
            Timestamp = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero),
            Payload = new UiBlockPayload { Block = block },
        });
    }
}
