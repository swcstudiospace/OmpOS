using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Autonogrammer.Native.Views;
using Autonogrammer.Native.Views.Blocks;
using Autonogrammer.Parity.Support;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Parity;

/// <summary>
/// Runs each shared session-chat scenario through the native <see cref="SessionView"/> and checks the normalized
/// rendering against the shared expectation (Phase 21 parity), the native counterpart of the Razor adapter in
/// <c>Autonogrammer.UI.Tests</c>. The expectations live in <c>Autonogrammer.Parity.Support</c>, not in this head.
/// </summary>
/// <remarks>
/// Normalization follows the Razor adapter. Visible text is the text of every text block in a block, in document order,
/// whitespace collapsed. Two things contribute no text, as the web head's controls and list numbering contribute none:
/// an icon control (a button whose accessible name is not its label, such as the copy button, like an element with
/// <c>aria-label</c>), and the marker of a list row (a bullet or step number). A disclosure contributes its header and
/// its content whether or not it is open, as a web <c>details</c> does.
/// </remarks>
public sealed class SharedScenarioParityTests
{
    /// <summary>A prompt and a streamed answer render as a user turn then one markdown block.</summary>
    [Fact]
    public async Task UserTurnWithMarkdownAnswer()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.UserTurnWithMarkdownAnswer()).ConfigureAwait(true);
    }

    /// <summary>Streamed text flushes into markdown before a code block that keeps its language label.</summary>
    [Fact]
    public async Task CodeBlockFlushesStreamedText()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.CodeBlockFlushesStreamedText()).ConfigureAwait(true);
    }

    /// <summary>A tool-call card shows its name and the status from its later update.</summary>
    [Fact]
    public async Task ToolCallCardTracksStatus()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.ToolCallCardTracksStatus()).ConfigureAwait(true);
    }

    /// <summary>A permission request shows its prompt, its options in order, and the read-only note.</summary>
    [Fact]
    public async Task PermissionRequestListsOptionsInOrder()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.PermissionRequestListsOptionsInOrder()).ConfigureAwait(true);
    }

    /// <summary>Plan steps render in order.</summary>
    [Fact]
    public async Task PlanStepsRenderInOrder()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.PlanStepsRenderInOrder()).ConfigureAwait(true);
    }

    /// <summary>Thoughts stay out of the rendered blocks.</summary>
    [Fact]
    public async Task ThoughtsStayOutOfTheView()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.ThoughtsStayOutOfTheView()).ConfigureAwait(true);
    }

    /// <summary>A retryable error keeps the partial output above its message.</summary>
    [Fact]
    public async Task RetryableErrorKeepsPartialOutput()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.RetryableErrorKeepsPartialOutput()).ConfigureAwait(true);
    }

    /// <summary>A fatal error renders its message as an error block.</summary>
    [Fact]
    public async Task FatalErrorShowsMessage()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.FatalErrorShowsMessage()).ConfigureAwait(true);
    }

    /// <summary>An unrecognised payload kind renders as a collapsed disclosure with its kind and raw JSON.</summary>
    [Fact]
    public async Task UnknownKindStaysCollapsed()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.UnknownKindStaysCollapsed()).ConfigureAwait(true);
    }

    /// <summary>A diff placeholder renders its path and reason.</summary>
    [Fact]
    public async Task DiffPlaceholderShowsPath()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.DiffPlaceholderShowsPath()).ConfigureAwait(true);
    }

    /// <summary>The A2UI surface renders its supported components in order and discloses the unsupported one.</summary>
    [Fact]
    public async Task A2uiSurfaceRendersSupportedAndDisclosesUnsupported()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.A2uiSurfaceRendersSupportedAndDisclosesUnsupported()).ConfigureAwait(true);
    }

    /// <summary>Streamed text that has not reached a block boundary renders after the flushed blocks.</summary>
    [Fact]
    public async Task StreamingTailAfterFlushedBlocks()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.StreamingTailAfterFlushedBlocks()).ConfigureAwait(true);
    }

    /// <summary>Two turns render in event order, each user turn before its own answer.</summary>
    [Fact]
    public async Task TwoTurnsKeepEventOrder()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.TwoTurnsKeepEventOrder()).ConfigureAwait(true);
    }

    /// <summary>A diagnostic is never rendered and does not split the streamed text.</summary>
    [Fact]
    public async Task DiagnosticsNeverRender()
    {
        await using var session = TestAppBuilder.StartSession();
        await AssertScenarioAsync(session, ParityScenarios.DiagnosticsNeverRender()).ConfigureAwait(true);
    }

    private static Task AssertScenarioAsync(HeadlessUnitTestSession session, ParityScenario scenario) =>
        session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            foreach (var evt in scenario.Events)
            {
                model.ApplyEvent(evt);
            }

            ParityNormalization.KindsOf(model.Tree)
                .ShouldBe(scenario.ExpectedBlocks.Select(block => block.Kind).ToArray(), $"{scenario.Name}: folded block kinds");

            using var host = new Host((SessionView)ViewMapping.Create(model));

            ReadRenderedBlocks(host.View).ShouldBe(scenario.ExpectedBlocks.ToArray(), $"{scenario.Name}: rendered blocks");
        }, TestContext.Current.CancellationToken);

    private static ParityBlock[] ReadRenderedBlocks(SessionView view)
    {
        var blockHost = view.FindControl<StackPanel>("BlockHost") ?? throw new InvalidOperationException("The transcript has no block host.");
        var blocks = blockHost.Children
            .Select(block => new ParityBlock(KindOf(block), VisibleText(block)))
            .ToList();

        var streaming = view.FindControl<TextBlock>("StreamingText") ?? throw new InvalidOperationException("The transcript has no streaming text.");
        if (streaming.IsVisible)
        {
            blocks.Add(new ParityBlock(ParityKinds.Streaming, ParityNormalization.CollapseWhitespace(streaming.Text ?? string.Empty)));
        }

        return blocks.ToArray();
    }

    private static string KindOf(Control block) => block switch
    {
        UserMessageView => ParityKinds.User,
        MarkdownBlockView => ParityKinds.Markdown,
        CodeBlockView => ParityKinds.Code,
        ToolCallBlockView => ParityKinds.ToolCall,
        PermissionBlockView => ParityKinds.Permission,
        PlanBlockView => ParityKinds.Plan,
        DiffPlaceholderView => ParityKinds.Diff,
        ErrorBlockView => ParityKinds.Error,
        A2uiSurfaceView => ParityKinds.A2ui,
        UnknownBlockView => ParityKinds.Unknown,
        _ => throw new InvalidOperationException($"Rendered block {block.GetType().Name} has no parity kind."),
    };

    private static string VisibleText(Visual root)
    {
        var text = new StringBuilder();
        AppendVisibleText(root, text);
        return ParityNormalization.CollapseWhitespace(text.ToString());
    }

    private static void AppendVisibleText(Visual node, StringBuilder text)
    {
        switch (node)
        {
            case Button button when IsIconControl(button):
                return;
            case TextBlock textBlock when IsListMarker(textBlock):
                return;
            case TextBlock textBlock:
                text.Append(' ').Append(InlineText(textBlock)).Append(' ');
                return;
            case Expander disclosure:
                // The header and the content are part of the block whether or not the disclosure is open.
                if (disclosure.Header is string header)
                {
                    text.Append(' ').Append(header).Append(' ');
                }
                else if (disclosure.Header is Visual headerVisual)
                {
                    AppendVisibleText(headerVisual, text);
                }

                if (disclosure.Content is Visual content)
                {
                    AppendVisibleText(content, text);
                }

                return;
            default:
                foreach (var child in node.GetVisualChildren())
                {
                    AppendVisibleText(child, text);
                }

                return;
        }
    }

    /// <summary>A button whose accessible name is not its visible label is an icon control, such as the copy button.</summary>
    private static bool IsIconControl(Button button)
    {
        var name = AutomationProperties.GetName(button);
        return !string.IsNullOrEmpty(name) && !string.Equals(name, button.Content as string, StringComparison.Ordinal);
    }

    /// <summary>
    /// The marker of a list row. <c>BlockControls.Row</c> lays a row out as a two-column grid whose first column is
    /// <c>Auto</c>, and puts the marker (a bullet or a step number) in that column.
    /// </summary>
    private static bool IsListMarker(TextBlock textBlock) =>
        textBlock.Parent is Grid grid
        && grid.ColumnDefinitions.Count == 2
        && grid.ColumnDefinitions[0].Width.IsAuto
        && grid.Children.Count == 2
        && Grid.GetColumn(textBlock) == 0;

    private static string InlineText(TextBlock textBlock) => textBlock.Text ?? InlineText(textBlock.Inlines ?? []);

    private static string InlineText(IEnumerable<Inline> inlines) => string.Concat(inlines.Select(static inline => inline switch
    {
        Run run => run.Text,
        Span span => InlineText(span.Inlines ?? []),
        LineBreak => "\n",
        _ => string.Empty,
    }));

    /// <summary>A shown window that hosts one transcript, closed when the test disposes it.</summary>
    private sealed class Host : IDisposable
    {
        public Host(SessionView view)
        {
            View = view;
            Window = new Window { Content = view, Width = 640, Height = 480 };
            Window.Show();
            ShellHarness.Settle(Window);
        }

        public SessionView View { get; }

        public Window Window { get; }

        public void Dispose() => Window.Close();
    }
}
