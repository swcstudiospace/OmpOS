using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>Keyboard reach, the visible focus style, and the composer key rules (XP-01, XP-08; criterion 5 and UI-SPEC Behaviour).</summary>
/// <remarks>
/// Keys are sent with <c>KeyPress</c> and its <see cref="Key"/> and <see cref="PhysicalKey"/>, never with
/// the text-input method, which raises text input only and does not move focus (RESEARCH pitfall 7).
/// Shift+Tab is sent as the raw modifier <see cref="KeyModifiers.Shift"/> converted to <c>RawInputModifiers</c>.
/// The two enums share their keyboard bits (<c>RawInputHelpers.ToKeyModifiers</c>), so this compiles without naming a
/// <c>RawInputModifiers</c> member that was not verified (OQ-31).
/// </remarks>
public sealed class KeyboardFocusTests
{
    /// <summary>Tab moves focus from the session list through the thread header to the pipeline strip, the transcript, the prompt, send and stop, in that order.</summary>
    [Fact]
    public async Task TabMovesFromSessionListThroughPipelineStripTranscriptPromptSendAndStop()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.Rows(window).Focus();

            var strip = ShellTree.NamedVisible<Button>(main, "PipelineStrip").ShouldNotBeNull();
            var visited = TabUntil(window, strip);
            visited.Select(control => AutomationProperties.GetName(control)).ShouldContain("Close this agent", StringComparer.Ordinal, "the header's Close control comes before the pipeline strip");

            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            ShellHarness.Scroll(window).IsFocused.ShouldBeTrue("the transcript follows the pipeline strip, which sits directly under the header");

            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue("the prompt follows the transcript");

            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            ShellHarness.Send(window).IsFocused.ShouldBeTrue("send follows the prompt");

            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            ShellHarness.Stop(window).IsFocused.ShouldBeTrue("stop follows send");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Shift+Tab moves focus back one shared control.</summary>
    [Fact]
    public async Task ShiftTabMovesFocusBack()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            ShellHarness.Stop(window).Focus();

            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab, shift: true);

            ShellHarness.Send(window).IsFocused.ShouldBeTrue("Shift+Tab from stop returns to send");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Each shared control that keyboard focus reaches shows the AccentPurple token as a 2 px border with no default adorner.
    /// Focus arrives by Tab presses, so the <c>:focus-visible</c> style applies.
    /// </summary>
    [Fact]
    public async Task KeyboardFocusedSharedControlsShowTheAccentRing()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            ShellHarness.SkipLink(window).Focus();

            var main = ShellHarness.MainViewOf(window);
            var order = new Control[]
            {
                main.FindControl<ToggleButton>("SessionsViewToggle")!,
                main.FindControl<ToggleButton>("ToolsViewToggle")!,
                ShellHarness.Rows(window),
                ShellTree.NamedVisible<Button>(main, "PipelineStrip")!,
                ShellHarness.Scroll(window),
                ShellHarness.Prompt(window),
                ShellHarness.Send(window),
                ShellHarness.Stop(window),
            };

            foreach (var control in order)
            {
                TabUntil(window, control);
                AssertAccentRing(control);
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter sends: the host refuses in Phase 20, so the status line says so and the draft is kept.</summary>
    [Fact]
    public async Task EnterSendsTheDraftAndTheRefusedSendKeepsIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShellHarness.Show();
            ShellHarness.Prompt(window).Text = "hello";
            ShellHarness.Prompt(window).Focus();

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).Text.ShouldBe(MainView.RefusedSendStatus);
            ShellHarness.Prompt(window).Text.ShouldBe("hello");
            ShellHarness.ComposerModelOf(window).Draft.ShouldBe("hello");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Shift+Enter does not send, so the status line is untouched.</summary>
    [Fact]
    public async Task ShiftEnterDoesNotSend()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShellHarness.Show();
            ShellHarness.Prompt(window).Text = "hello";
            ShellHarness.Prompt(window).CaretIndex = "hello".Length;
            ShellHarness.Prompt(window).Focus();

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter, shift: true);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).Text.ShouldBeNullOrWhiteSpace();
            ShellHarness.ComposerModelOf(window).Draft.ShouldStartWith("hello");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on a blank draft is a no-op: the draft and the focus stay, and nothing is reported.</summary>
    [Fact]
    public async Task EnterOnABlankDraftIsANoOp()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShellHarness.Show();
            ShellHarness.Prompt(window).Text = "   ";
            ShellHarness.Prompt(window).Focus();

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).Text.ShouldBeNullOrWhiteSpace();
            ShellHarness.Prompt(window).Text.ShouldBe("   ");
            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Escape has no effect when no response is running (UI-SPEC, Behaviour Contract).</summary>
    [Fact]
    public async Task EscapeHasNoEffectWhenNothingRuns()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShellHarness.Show();
            ShellHarness.Prompt(window).Text = "hello";
            ShellHarness.Prompt(window).Focus();

            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).Text.ShouldBeNullOrWhiteSpace();
            ShellHarness.Prompt(window).Text.ShouldBe("hello");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on the send control refuses the send and keeps the draft.</summary>
    [Fact]
    public async Task EnterOnTheSendControlRefusesAndKeepsTheDraft()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShellHarness.Show();
            ShellHarness.Prompt(window).Text = "hello";
            ShellHarness.Send(window).Focus();

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).Text.ShouldBe(MainView.RefusedSendStatus);
            ShellHarness.ComposerModelOf(window).Draft.ShouldBe("hello");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on stop with nothing running reports it, and both controls stay enabled.</summary>
    [Fact]
    public async Task EnterOnStopReportsNoResponseAndStaysEnabled()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShellHarness.Show();
            ShellHarness.Stop(window).Focus();

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).Text.ShouldBe(MainView.NoResponseStatus);
            ShellHarness.Stop(window).IsEnabled.ShouldBeTrue();
            ShellHarness.Send(window).IsEnabled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Presses Tab until <paramref name="target"/> has focus, and returns the controls focus visited on the way, target last.</summary>
    private static List<Control> TabUntil(MainWindow window, Control target)
    {
        const int MaxSteps = 16;
        var visited = new List<Control>();
        for (var step = 0; step < MaxSteps && !target.IsFocused; step++)
        {
            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            if (TopLevel.GetTopLevel(target)?.FocusManager?.GetFocusedElement() is Control focused)
            {
                visited.Add(focused);
            }
        }

        target.IsFocused.ShouldBeTrue($"{target.GetType().Name} must be reached by Tab within {MaxSteps} presses");
        return visited;
    }

    private static void AssertAccentRing(Control control)
    {
        var application = Application.Current!;
        // cursor-agents-window-parity.md 3.13: primary buttons sit on the accent fill, so their 2 px ring is the TextPrimary token.
        var token = control.Classes.Contains("primary") ? "Autonogrammer.Color.TextPrimary" : "Autonogrammer.Color.AccentPurple";
        application.TryGetResource(token, application.ActualThemeVariant, out var accentBrush).ShouldBeTrue("the ring token must resolve for the active theme variant");
        var accent = ((ISolidColorBrush)accentBrush!).Color;

        var templated = (TemplatedControl)control;
        var border = templated.BorderBrush as ISolidColorBrush;
        border.ShouldNotBeNull($"{control.GetType().Name} must have a solid focus border");
        border.Color.ShouldBe(accent, $"{control.GetType().Name} focus border must be the {token} token");
        templated.BorderThickness.ShouldBe(new Thickness(2), $"{control.GetType().Name} focus border must be Autonogrammer.Size.FocusBorder");
        control.FocusAdorner.ShouldBeNull($"{control.GetType().Name} must not draw the default focus adorner");
    }
}
