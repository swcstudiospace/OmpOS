using System.Text;
using AngleSharp.Dom;
using Autonogrammer.Parity.Support;
using Autonogrammer.Presentation.State;
using Autonogrammer.UI.Components;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>
/// Runs each shared session-chat scenario through the Razor <see cref="SessionView"/> and checks the normalized
/// rendering against the shared expectation (Phase 21 parity). The expectations live in
/// <c>Autonogrammer.Parity.Support</c>, not in this head.
/// </summary>
public sealed class SharedScenarioParityTests : UiTestContext
{
    private static readonly (string ClassName, string Kind)[] KindClasses =
    [
        ("autonogrammer-streaming", ParityKinds.Streaming),
        ("autonogrammer-user", ParityKinds.User),
        ("autonogrammer-markdown", ParityKinds.Markdown),
        ("autonogrammer-code", ParityKinds.Code),
        ("autonogrammer-tool-call", ParityKinds.ToolCall),
        ("autonogrammer-tool-result", ParityKinds.ToolResult),
        ("autonogrammer-file-edit", ParityKinds.FileEdit),
        ("autonogrammer-permission", ParityKinds.Permission),
        ("autonogrammer-plan", ParityKinds.Plan),
        ("autonogrammer-diff", ParityKinds.Diff),
        ("autonogrammer-error", ParityKinds.Error),
        ("autonogrammer-a2ui", ParityKinds.A2ui),
        ("autonogrammer-unknown", ParityKinds.Unknown),
    ];

    /// <summary>A prompt and a streamed answer render as a user turn then one markdown block.</summary>
    [Fact]
    public void UserTurnWithMarkdownAnswer() => AssertScenario(ParityScenarios.UserTurnWithMarkdownAnswer());

    /// <summary>Streamed text flushes into markdown before a code block that keeps its language label.</summary>
    [Fact]
    public void CodeBlockFlushesStreamedText() => AssertScenario(ParityScenarios.CodeBlockFlushesStreamedText());

    /// <summary>A tool-call card shows its name and the status from its later update.</summary>
    [Fact]
    public void ToolCallCardTracksStatus() => AssertScenario(ParityScenarios.ToolCallCardTracksStatus());

    /// <summary>A permission request shows its prompt, its options in order, and the read-only note.</summary>
    [Fact]
    public void PermissionRequestListsOptionsInOrder() => AssertScenario(ParityScenarios.PermissionRequestListsOptionsInOrder());

    /// <summary>A recorded approval decision shows as text on the permission block.</summary>
    [Fact]
    public void PermissionDecisionShowsOutcome() => AssertScenario(ParityScenarios.PermissionDecisionShowsOutcome());

    /// <summary>A tool update's output renders as a result block after the call card.</summary>
    [Fact]
    public void ToolResultShowsOutputAfterItsCall() => AssertScenario(ParityScenarios.ToolResultShowsOutputAfterItsCall());

    /// <summary>Diff entries render as file-edit blocks with the path and both texts.</summary>
    [Fact]
    public void FileEditShowsPathAndBothTexts() => AssertScenario(ParityScenarios.FileEditShowsPathAndBothTexts());

    /// <summary>Plan steps render in order.</summary>
    [Fact]
    public void PlanStepsRenderInOrder() => AssertScenario(ParityScenarios.PlanStepsRenderInOrder());

    /// <summary>Thoughts stay out of the rendered blocks.</summary>
    [Fact]
    public void ThoughtsStayOutOfTheView() => AssertScenario(ParityScenarios.ThoughtsStayOutOfTheView());

    /// <summary>A retryable error keeps the partial output above its message.</summary>
    [Fact]
    public void RetryableErrorKeepsPartialOutput() => AssertScenario(ParityScenarios.RetryableErrorKeepsPartialOutput());

    /// <summary>A fatal error renders its message as an error block.</summary>
    [Fact]
    public void FatalErrorShowsMessage() => AssertScenario(ParityScenarios.FatalErrorShowsMessage());

    /// <summary>An unrecognised payload kind renders as a collapsed disclosure with its kind and raw JSON.</summary>
    [Fact]
    public void UnknownKindStaysCollapsed() => AssertScenario(ParityScenarios.UnknownKindStaysCollapsed());

    /// <summary>A diff placeholder renders its path and reason.</summary>
    [Fact]
    public void DiffPlaceholderShowsPath() => AssertScenario(ParityScenarios.DiffPlaceholderShowsPath());

    /// <summary>The A2UI surface renders its supported components in order and discloses the unsupported one.</summary>
    [Fact]
    public void A2uiSurfaceRendersSupportedAndDisclosesUnsupported() =>
        AssertScenario(ParityScenarios.A2uiSurfaceRendersSupportedAndDisclosesUnsupported());

    /// <summary>Streamed text that has not reached a block boundary renders after the flushed blocks.</summary>
    [Fact]
    public void StreamingTailAfterFlushedBlocks() => AssertScenario(ParityScenarios.StreamingTailAfterFlushedBlocks());

    /// <summary>Two turns render in event order, each user turn before its own answer.</summary>
    [Fact]
    public void TwoTurnsKeepEventOrder() => AssertScenario(ParityScenarios.TwoTurnsKeepEventOrder());

    /// <summary>A diagnostic is never rendered and does not split the streamed text.</summary>
    [Fact]
    public void DiagnosticsNeverRender() => AssertScenario(ParityScenarios.DiagnosticsNeverRender());

    private void AssertScenario(ParityScenario scenario)
    {
        var model = new SessionViewModel();
        foreach (var evt in scenario.Events)
        {
            model.ApplyEvent(evt);
        }

        ParityNormalization.KindsOf(model.Tree)
            .ShouldBe(scenario.ExpectedBlocks.Select(block => block.Kind).ToArray(), $"{scenario.Name}: folded block kinds");

        var view = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, model.Tree)
            .Add(p => p.ViewState, model.State));

        ReadRenderedBlocks(view).ShouldBe(scenario.ExpectedBlocks.ToArray(), $"{scenario.Name}: rendered blocks");
    }

    private static ParityBlock[] ReadRenderedBlocks(IRenderedComponent<SessionView> view) =>
        view.FindAll(".autonogrammer-blocks > *")
            .Select(element => new ParityBlock(KindOf(element), VisibleText(element)))
            .ToArray();

    private static string KindOf(IElement element)
    {
        var classes = (element.ClassName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var (className, kind) in KindClasses)
        {
            if (classes.Contains(className, StringComparer.Ordinal))
            {
                return kind;
            }
        }

        throw new InvalidOperationException($"Rendered block <{element.LocalName} class='{element.ClassName}'> has no parity kind.");
    }

    private static string VisibleText(IElement element)
    {
        var text = new StringBuilder();
        AppendVisibleText(element, text);
        return ParityNormalization.CollapseWhitespace(text.ToString());
    }

    private static void AppendVisibleText(INode node, StringBuilder text)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText textNode)
            {
                text.Append(textNode.Data);
            }
            else if (child is IElement element && !IsControl(element))
            {
                text.Append(' ');
                AppendVisibleText(element, text);
                text.Append(' ');
            }
        }
    }

    private static bool IsControl(IElement element) => !string.IsNullOrEmpty(element.GetAttribute("aria-label"));
}
