using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.AgentPanel;
using Avalonia.Automation;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Views;

/// <summary>The thread block of the panel states Loading and Error (46-UI-SPEC, States). Empty is the Code landing's.</summary>
public sealed class PanelStateViewTests : IDisposable
{
    private readonly List<IDisposable> _owned = [];

    /// <summary>Disposes the catalog models the test opened.</summary>
    public void Dispose()
    {
        foreach (var owned in _owned)
        {
            owned.Dispose();
        }
    }

    private static SessionCatalogModel CatalogOf(FakeSessionCatalog fake, ManualTimeProvider clock) => PanelFixtures.Catalog(fake, clock);

    private (PanelStateView View, AgentPanelViewModel Panel, ScriptedPanelHost Host, FakeSessionCatalog Fake, ManualTimeProvider Clock, Window Window) Open()
    {
        var clock = new ManualTimeProvider();
        var fake = new FakeSessionCatalog();
        var catalog = CatalogOf(fake, clock);
        _owned.Add(catalog);
        var host = new ScriptedPanelHost(fake, clock);
        var panel = new AgentPanelViewModel(catalog, host);
        var view = new PanelStateView { DataContext = panel };
        var window = ViewHarness.Show(view);
        window.Closed += (_, _) => panel.Dispose();
        return (view, panel, host, fake, clock, window);
    }

    /// <summary>With no sessions the block draws nothing: the Code landing takes the empty state, so there is one start surface, not two.</summary>
    [Fact]
    public async Task EmptyStateDrawsNothing()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, panel, _, _, _, window) = Open();

            panel.State.ShouldBe(PanelState.Empty);
            view.IsVisible.ShouldBeFalse();
            ViewHarness.VisibleTexts(view).ShouldBeEmpty();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>While the panel initialises the block draws static loading text, never a spinner, and stays out of the tab order.</summary>
    [Fact]
    public async Task LoadingStateRendersItsCopy()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, panel, _, _, _, window) = Open();

            using var scope = panel.BeginInitialising();
            ViewHarness.Settle(window);

            panel.State.ShouldBe(PanelState.Loading);
            var texts = ViewHarness.VisibleTexts(view);
            texts.ShouldContain(AgentPanelCopy.PanelLoadingHeading, StringComparer.Ordinal);
            texts.ShouldContain(AgentPanelCopy.PanelLoadingBody, StringComparer.Ordinal);
            texts.ShouldNotContain(AgentPanelCopy.EmptyThreadHeading, StringComparer.Ordinal);
            ViewHarness.TabOrder(view).ShouldBeEmpty("the loading block has no control");
            ViewHarness.Unnamed(view).ShouldBeEmpty();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A failed load draws the assertive Panel error with its banner text and a Retry loading button that reloads.</summary>
    [Fact]
    public async Task PanelErrorOffersRetry()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (view, panel, host, _, _, window) = Open();

            panel.ReportLoadFailure("The registry file is unreadable");
            ViewHarness.Settle(window);

            panel.State.ShouldBe(PanelState.Error);
            var error = ViewHarness.Find<Border>(view, "PanelError");
            error.IsEffectivelyVisible.ShouldBeTrue();
            AutomationProperties.GetName(error).ShouldBe("Panel error");
            AutomationProperties.GetLiveSetting(error).ShouldBe(AutomationLiveSetting.Assertive);
            ViewHarness.VisibleTexts(view).ShouldContain(panel.BannerText!, StringComparer.Ordinal);
            var retry = ViewHarness.Find<Button>(view, "RetryLoadingButton");
            retry.Content.ShouldBe(AgentPanelCopy.RetryLoading);
            AutomationProperties.GetName(retry).ShouldBe(AgentPanelCopy.RetryLoadingName);
            ViewHarness.TabOrder(view).ShouldBe(new[] { AgentPanelCopy.RetryLoadingName });

            ViewHarness.Click(window, retry);
            for (var attempt = 0; attempt < 50 && host.ReloadCount == 0; attempt++)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken).ConfigureAwait(true);
            }

            host.ReloadCount.ShouldBe(1);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Once an agent exists the block hides itself, so nothing of it is a tab stop.</summary>
    [Fact]
    public async Task ReadyStateHidesTheBlock()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (view, panel, _, _, _, window) = Open();

            await panel.NewSessionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            ViewHarness.Settle(window);

            panel.State.ShouldBe(PanelState.Ready);
            view.IsVisible.ShouldBeFalse();
            ViewHarness.TabOrder(view).ShouldBeEmpty();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A null data context hides the block and a swap redraws it, without leaving a subscription on the old panel.</summary>
    [Fact]
    public async Task ADataContextSwapRedrawsWithoutLeakingTheOldPanel()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, first, _, _, clock, window) = Open();
            var otherFake = new FakeSessionCatalog();
            var otherCatalog = PanelFixtures.Catalog(otherFake, clock);
            var other = new AgentPanelViewModel(otherCatalog, new ScriptedPanelHost(otherFake, clock));

            view.DataContext = null;
            ViewHarness.Settle(window);
            view.IsVisible.ShouldBeFalse();

            view.DataContext = other;
            ViewHarness.Settle(window);
            other.State.ShouldBe(PanelState.Empty);
            view.IsVisible.ShouldBeFalse("an empty panel draws nothing: the Code landing takes its place");

            first.ReportLoadFailure("Old panel failed");
            ViewHarness.Settle(window);
            view.IsVisible.ShouldBeFalse("the old panel no longer drives the block");

            other.ReportLoadFailure("New panel failed");
            ViewHarness.Settle(window);
            ViewHarness.Find<Border>(view, "PanelError").IsEffectivelyVisible.ShouldBeTrue();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
