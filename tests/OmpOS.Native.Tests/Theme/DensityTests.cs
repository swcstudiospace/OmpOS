using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using OmpOS.Native.Tests.Kanban;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Theme;

/// <summary>
/// The density contract of Phase 50 (OQ-87, spec 2.5): <c>OmpOS.Size.TargetMin</c> is 32, every interactive control the shell and the
/// Kanban tab draw is at least the WCAG 2.2 AA target (24 by 24), and every text button and input keeps a <c>MinHeight</c> of at least
/// the token (or the 28 px and 24 px control heights the specification gives the small, nav and segment variants), so one value in the token file sets the height of the whole desktop.
/// </summary>
public sealed class DensityTests
{
    /// <summary>The desktop token is 32; the control heights of the spec sit at or above the WCAG minimum.</summary>
    [Fact]
    public async Task TheDesktopTokensAreDenseButNotBelowTheWcagMinimum()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            Token("OmpOS.Size.TargetMin").ShouldBe(32);
            Token("OmpOS.Size.ControlHeight").ShouldBe(32);
            Token("OmpOS.Size.ControlHeightSm").ShouldBe(28);
            Token("OmpOS.Size.IconButton").ShouldBe(28);
            Token("OmpOS.Size.IconButtonSm").ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            Token("OmpOS.Size.TargetMin").ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>In the shell, in the Sessions view and in the Tools view, every interactive control is at least 24 by 24 and every text button and input has the token as its minimum height.</summary>
    [Fact]
    public async Task EveryShellControlMeetsTheTargetAndTheTokenMinimum()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var problems = new List<string>();

            Audit(window, "sessions view", problems);
            ShellHarness.ShowToolsView(window);
            Audit(window, "tools view", problems);

            problems.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>On the Kanban tab, with cards, every interactive control is at least 24 by 24 and every text button has the token as its minimum height.</summary>
    [Fact]
    public async Task EveryKanbanControlMeetsTheTargetAndTheTokenMinimum()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open();
            var problems = new List<string>();

            Audit(fixture.Window, "kanban board", problems);

            problems.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static double Token(string key)
    {
        Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var value).ShouldBeTrue($"{key} must resolve");
        return (double)value!;
    }

    /// <summary>
    /// The token minimum of one control. Cursor-agents-window-parity.md 3.3, 3.4, 3.6 and 3.7 fix 28 px for the <c>sm</c> buttons and
    /// inputs, the <c>nav</c> toggles and the top tab items (<c>OmpOS.Size.ControlHeightSm</c>), and 24 px for the <c>segment</c>
    /// toggles (the WCAG 2.2 minimum, <c>OmpOS.Size.IconButtonSm</c>); every other text control keeps <c>TargetMin</c>.
    /// </summary>
    private static double FloorOf(Control control, double tokenMinimum)
    {
        if (control.Classes.Contains("segment"))
        {
            return Token("OmpOS.Size.IconButtonSm");
        }

        return control.Classes.Contains("sm") || control.Classes.Contains("nav") || control is TabStripItem
            ? Token("OmpOS.Size.ControlHeightSm")
            : tokenMinimum;
    }

    private static void Audit(Window window, string where, List<string> problems)
    {
        var tokenMinimum = Token("OmpOS.Size.TargetMin");
        var controls = window.GetVisualDescendants()
            .OfType<Control>()
            .Where(static control => control.TemplatedParent is null && control.IsEffectivelyVisible)
            .Where(static control => control is Button or ToggleButton or TextBox or TabStripItem or ComboBox)
            .ToList();
        controls.ShouldNotBeEmpty($"{where} must draw some controls");

        foreach (var control in controls)
        {
            var name = $"{where}: {control.GetType().Name} {control.Name} {(control as ContentControl)?.Content as string}".TrimEnd();
            if (control.Bounds.Width < TargetSize.MinimumTarget || control.Bounds.Height < TargetSize.MinimumTarget)
            {
                problems.Add($"{name} is {control.Bounds.Width:0.#} by {control.Bounds.Height:0.#}");
            }

            var isTextControl = control is TextBox or ComboBox || (control is ContentControl { Content: string });
            var floor = FloorOf(control, tokenMinimum);
            if (isTextControl && control.MinHeight < floor)
            {
                problems.Add($"{name} has MinHeight {control.MinHeight:0.#}, below its token minimum {floor:0.#}");
            }
        }
    }
}
