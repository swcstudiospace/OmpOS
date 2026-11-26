using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Views;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>
/// A persisted terminal row in the shell (Phase 81): it stays listed as terminal, but opening it creates a retired runtime — no starter,
/// transport or engine exists anymore — the thread shows a plain transcript instead of a terminal, the composer hides, and the retired banner
/// explains why. Switching sessions and tabs, opening from a Kanban card, and restoring it closed all stay retired.
/// </summary>
public sealed class AgentPanelTerminalSessionTests
{
    private static readonly string[] FontTokens = ["Autonogrammer.Font.Family", "Autonogrammer.Font.Display", "Autonogrammer.Font.Mono"];

    /// <summary>A persisted terminal row opens as retired: a plain transcript in the thread, and the retired banner.</summary>
    [Fact]
    public async Task APersistedTerminalOpensAsRetired()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);

            var (runtime, channel) = await OpenRetiredAsync(fixture, window, token).ConfigureAwait(true);

            runtime.IsRetiredTerminal.ShouldBeTrue();
            runtime.Kind.ShouldBe(SessionKind.Terminal);
            channel.Sent.ShouldBeEmpty();

            fixture.Panel.Selected.ShouldNotBeNull().Row.IsTerminal.ShouldBeTrue("a persisted terminal row stays listed as terminal");
            var thread = main.FindControl<ContentControl>("TranscriptHost")!.Content;
            thread.ShouldBeOfType<SessionView>();
            var composer = main.FindControl<ContentControl>("ComposerHost")!;
            composer.Content.ShouldBeOfType<ComposerView>();
            composer.IsVisible.ShouldBeFalse("a retired terminal has no steer composer");

            var notice = ShellTree.NamedVisible<Border>(main, "ClosedNotice");
            notice.ShouldNotBeNull("the retired banner shows");
            main.FindControl<TextBlock>("ClosedHeading")!.Text.ShouldBe(AgentPanelCopy.RetiredTerminalHeading);
            main.FindControl<TextBlock>("ClosedBody")!.Text.ShouldBe(AgentPanelCopy.RetiredTerminalBody + " " + AgentPanelCopy.RetiredTerminalSteeringUnavailable);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A persisted terminal row carries the Terminal tag, Retired status and a terminal accessible name; the rail does not offer New omp terminal.</summary>
    [Fact]
    public async Task APersistedTerminalRowIsTaggedTerminal()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddTerminalSession("terminal-1", "Shell");
            var window = ShellHarness.Show(fixture);
            fixture.Panel.SelectSession("terminal-1");
            await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "terminal-1", StringComparison.Ordinal), "the terminal session", token).ConfigureAwait(true);

            var rail = ShellHarness.Rows(window);
            ShellTree.VisibleTexts(rail).Count(text => string.Equals(text, "Terminal", StringComparison.Ordinal)).ShouldBe(1, "only the terminal row is tagged");
            var selected = fixture.Panel.Selected.ShouldNotBeNull();
            selected.Row.AccessibleName.ShouldContain(", terminal", Case.Sensitive);
            selected.Row.StatusText.ShouldBe(AgentPanelCopy.RetiredTerminalStatus, "a persisted terminal lists as retired, never its lifecycle status");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Selecting a chat and coming back, and visiting the Kanban tab, keep the retired terminal retired: the same transcript view is kept,
    /// and the retired banner stays.
    /// </summary>
    [Fact]
    public async Task SwitchingSessionsAndTabsKeepsTheTerminalRetired()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddTerminalSession("terminal-1", "Shell", new SessionScript { Steering = new FakeSteeringChannel() });
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            fixture.Panel.SelectSession("terminal-1");
            await ShellWait.UntilAsync(window, () => fixture.Host.Runtimes.ContainsKey("terminal-1") && fixture.Runtime("terminal-1").IsRetiredTerminal, "the retired runtime", token).ConfigureAwait(true);
            ShellHarness.Settle(window);
            var thread = main.FindControl<ContentControl>("TranscriptHost")!.Content.ShouldBeOfType<SessionView>();

            fixture.Panel.SelectSession("session-1");
            ShellHarness.Settle(window);
            main.FindControl<ContentControl>("TranscriptHost")!.Content.ShouldNotBeSameAs(thread);
            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            BoardShell.Chord(window, Key.A, PhysicalKey.A);
            fixture.Panel.SelectSession("terminal-1");
            ShellHarness.Settle(window);

            main.FindControl<ContentControl>("TranscriptHost")!.Content.ShouldBeSameAs(thread, "the transcript view is kept with its session");
            fixture.Runtime("terminal-1").IsRetiredTerminal.ShouldBeTrue();
            ShellTree.NamedVisible<Border>(main, "ClosedNotice").ShouldNotBeNull("the retired banner stays");

            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            ShellHarness.Settle(window);

            main.IsKanbanShown.ShouldBeTrue("the Kanban surface replaces the transcript thread, hiding the retired banner");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With the Kanban tab shown first, a retired terminal session selected behind it stays retired when the Agents tab returns, with no error about a missing emulator.</summary>
    [Fact]
    public async Task ATerminalSelectedWhileTheKanbanTabIsShownStaysRetired()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddTerminalSession("terminal-1", "Shell");
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            main.IsKanbanShown.ShouldBeTrue();

            fixture.Panel.SelectSession("terminal-1");
            await ShellWait.UntilAsync(window, () => fixture.Host.Runtimes.ContainsKey("terminal-1"), "the terminal runtime", token).ConfigureAwait(true);
            ShellHarness.Settle(window);
            var (runtime, _) = Parts(fixture, "terminal-1");

            runtime.IsRetiredTerminal.ShouldBeTrue();
            main.FindControl<ContentControl>("TranscriptHost")!.Content.ShouldBeOfType<SessionView>().IsEffectivelyVisible.ShouldBeFalse("its region is hidden behind the Kanban tab");

            BoardShell.Chord(window, Key.A, PhysicalKey.A);
            ShellHarness.Settle(window);

            ShellTree.NamedVisible<Border>(main, "ClosedNotice").ShouldNotBeNull("the retired banner shows");
            main.FindControl<ContentControl>("ComposerHost")!.IsVisible.ShouldBeFalse("a retired terminal has no steer composer");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on the card of a persisted terminal session opens it in the Agents tab as retired: only the retired banner.</summary>
    [Fact]
    public async Task EnterOnATerminalCardOpensItAsRetired()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddTerminalSession("terminal-1", "Shell");
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            var card = BoardShell.Board(main).CardViewOf("terminal-1").ShouldNotBeNull();
            card.Model!.Card.KindText.ShouldBe("Terminal");
            card.Focus();

            ViewHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellWait.UntilAsync(window, () => fixture.Host.Runtimes.ContainsKey("terminal-1") && fixture.Runtime("terminal-1").IsRetiredTerminal && !main.IsKanbanShown, "the retired runtime to be shown", token).ConfigureAwait(true);

            var (runtime, _) = Parts(fixture, "terminal-1");
            runtime.IsRetiredTerminal.ShouldBeTrue();
            var thread = main.FindControl<ContentControl>("TranscriptHost")!.Content;
            thread.ShouldBeOfType<SessionView>();
            ShellTree.NamedVisible<Border>(main, "ClosedNotice").ShouldNotBeNull("the retired banner shows");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal session restored closed still lists as retired: the retired banner (not the closed one) shows.</summary>
    [Fact]
    public async Task ATerminalRestoredClosedListsAsRetired()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddTerminalSession("terminal-1", "Shell", lifecycle: SessionLifecycle.Paused);
            fixture.Host.ClosedIds.Add("terminal-1");
            fixture.Catalog.Refresh();
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            fixture.Panel.SelectSession("terminal-1");
            await ShellWait.UntilAsync(window, () => fixture.Host.Runtimes.ContainsKey("terminal-1") && fixture.Runtime("terminal-1").IsRetiredTerminal, "the retired runtime", token).ConfigureAwait(true);
            var (runtime, _) = Parts(fixture, "terminal-1");

            runtime.IsRetiredTerminal.ShouldBeTrue();
            ShellTree.NamedVisible<Border>(main, "ClosedNotice").ShouldNotBeNull("the retired banner shows");
            main.FindControl<TextBlock>("ClosedHeading")!.Text.ShouldBe(AgentPanelCopy.RetiredTerminalHeading, "retired wins over closed for a persisted terminal");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The accessibility walk over a retired terminal session: every authored stop has a name, and there is no steer input or steer button at all.</summary>
    [Fact]
    public async Task EveryAuthoredStopOfARetiredTerminalSessionHasAName()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            await OpenRetiredAsync(fixture, window, token).ConfigureAwait(true);
            var main = ShellHarness.MainViewOf(window);

            ViewHarness.Unnamed(main).ShouldBeEmpty();
            main.FindControl<TextBox>("MessageInput").ShouldBeNull("a retired terminal has no steer input");
            main.FindControl<Button>("SendNowButton").ShouldBeNull("a retired terminal has no steer buttons");
            main.FindControl<Button>("QueueButton").ShouldBeNull();
            ShellTree.NamedVisible<Border>(main, "ClosedNotice").ShouldNotBeNull("the retired banner shows");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Every text of the Agents layout with a terminal session, and of the Kanban tab, is drawn in one of the three Greptile families through the Autonogrammer.Font tokens.</summary>
    [Fact]
    public async Task EveryTextIsDrawnInAFontFromTheTokens()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddTerminalSession("terminal-1", "Shell");
            var main = fixture.ShowWithBoard();
            var window = fixture.BoardWindow!;
            fixture.Panel.SelectSession("terminal-1");
            await ShellWait.UntilAsync(window, () => fixture.Host.Runtimes.ContainsKey("terminal-1"), "the terminal runtime", token).ConfigureAwait(true);
            ShellHarness.Settle(window);
            var families = FontTokens
                .Select(key => ((FontFamily)Application.Current!.FindResource(key)!).Name)
                .ToHashSet(StringComparer.Ordinal);

            Fonts(main).Except(families, StringComparer.Ordinal).ShouldBeEmpty("the Agents tab draws only token fonts");

            BoardShell.Chord(window, Key.K, PhysicalKey.K);
            ShellHarness.Settle(window);

            Fonts(main).Except(families, StringComparer.Ordinal).ShouldBeEmpty("the Kanban tab draws only token fonts");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static HashSet<string> Fonts(Control root) =>
        root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static block => block.IsEffectivelyVisible && !string.IsNullOrEmpty(block.Text))
            .Select(static block => block.FontFamily.Name)
            .ToHashSet(StringComparer.Ordinal);

    private static (AgentSessionRuntime Runtime, FakeSteeringChannel Channel) Parts(ShellFixture fixture, string id)
    {
        var script = fixture.Script(id);
        return (fixture.Runtime(id), (FakeSteeringChannel)script.Steering!);
    }

    private static async Task<(AgentSessionRuntime Runtime, FakeSteeringChannel Channel)> OpenRetiredAsync(
        ShellFixture fixture,
        MainWindow window,
        CancellationToken token)
    {
        fixture.AddTerminalSession("terminal-1", "Shell");
        fixture.Panel.SelectSession("terminal-1");
        await ShellWait.UntilAsync(window, () => fixture.Host.Runtimes.ContainsKey("terminal-1") && fixture.Runtime("terminal-1").IsRetiredTerminal, "the retired runtime", token).ConfigureAwait(true);
        return Parts(fixture, "terminal-1");
    }
}
