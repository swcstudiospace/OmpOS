using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>Explicit accessible names on every control (XP-01, XP-08, D-20-09; criterion 4, headless half).</summary>
public sealed class AccessibilityNameTests
{
    private static readonly string[] ExpectedRowNames =
    {
        "Plan the release notes for the native head",
        "Review the accessibility baseline",
    };

    /// <summary>The six Control Inventory rows carry the UI-SPEC names verbatim.</summary>
    [Fact]
    public async Task InventoryControlsCarryTheUiSpecNames()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();

            AutomationProperties.GetName(ShellHarness.Rows(window)).ShouldBe("Agent sessions");
            AutomationProperties.GetName(ShellHarness.Scroll(window)).ShouldBe("Session transcript");
            AutomationProperties.GetName(ShellHarness.Prompt(window)).ShouldBe("Message");
            AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Send message");
            AutomationProperties.GetName(ShellHarness.Stop(window)).ShouldBe("Stop response");
            AutomationProperties.GetName(ShellHarness.Status(window)).ShouldBe("Session status");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The status line is the polite live region (UI-SPEC, Control Inventory row 6).</summary>
    [Fact]
    public async Task StatusLineIsAPoliteLiveRegion()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();

            AutomationProperties.GetLiveSetting(ShellHarness.Status(window)).ShouldBe(AutomationLiveSetting.Polite);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Walks the authored controls of the shell (not the parts of their templates). Every control a Tab press can reach,
    /// and every live region, has a non-empty accessible name.
    /// </summary>
    [Fact]
    public async Task EveryAuthoredTabStopAndLiveRegionHasAName()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();

            var unnamed = window.GetVisualDescendants()
                .OfType<Control>()
                .Where(static control => control.TemplatedParent is null)
                .Where(static control => (control.Focusable && control.IsTabStop)
                    || AutomationProperties.GetLiveSetting(control) is AutomationLiveSetting.Polite or AutomationLiveSetting.Assertive)
                .Where(static control => string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
                .Select(static control => control.GetType().Name)
                .ToList();

            unnamed.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The skip link is the first authored tab stop and carries the web head's copy.</summary>
    [Fact]
    public async Task SkipLinkCarriesTheWebCopy()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();

            AutomationProperties.GetName(ShellHarness.SkipLink(window)).ShouldBe("Skip to conversation");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Each agent row is named by its full title, its status and its last activity, not by a default control name.</summary>
    [Fact]
    public async Task EachSessionRowIsNamedByItsFullTitle()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            fixture.AddSession("a", ExpectedRowNames[0]);
            fixture.AddSession("b", ExpectedRowNames[1]);
            var window = ShellHarness.Show(fixture);

            var names = ShellHarness.Rows(window).GetVisualDescendants().OfType<ListBoxItem>()
                .Select(row => AutomationProperties.GetName(row) ?? string.Empty)
                .ToList();

            foreach (var title in ExpectedRowNames)
            {
                names.Count(name => name.StartsWith($"{title},", StringComparison.Ordinal)).ShouldBe(1, $"the row of '{title}' is named by its full title, then its status");
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
