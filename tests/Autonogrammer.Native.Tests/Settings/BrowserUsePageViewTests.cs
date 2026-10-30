using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.BrowserUse;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The Browser page view in a headless window: state sentences and the acquisition dialog.</summary>
public sealed class BrowserUsePageViewTests
{
    /// <summary>Each Chromium state is drawn, and the install action stays hidden without a manifest row.</summary>
    [Theory]
    [InlineData(BrowserUseChromiumState.BrowserNotInstalled, null, BrowserUseCopy.BrowserNotInstalledSentence, BrowserUseCopy.BrowserNotInstalledAction)]
    [InlineData(BrowserUseChromiumState.ManagedBrowserNotPinned, null, BrowserUseCopy.ManagedBrowserNotPinnedSentence, BrowserUseCopy.ManagedBrowserNotPinnedAction)]
    [InlineData(BrowserUseChromiumState.BrowserTooOld, null, BrowserUseCopy.BrowserTooOldSentence, BrowserUseCopy.BrowserTooOldAction)]
    [InlineData(BrowserUseChromiumState.Ready, "149", "Chromium 149 is ready.", BrowserUseCopy.ReadyAction)]
    public async Task DrawsEachStateAndHidesAcquisitionWithoutARow(
        BrowserUseChromiumState state,
        string? version,
        string sentence,
        string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(async () =>
        {
            var page = new BrowserUseSettingsPage(new ViewBrowserSource(state, version, offer: null));
            await page.LoadAsync(ct).ConfigureAwait(true);
            var window = Show(page, out var view);
            try
            {
                SettingsTree.Texts(view).ShouldContain(sentence);
                view.Fix.Content.ShouldBe(action);
                view.Acquire.IsVisible.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The dialog names version, host, size and licence, and Cancel does not acquire.</summary>
    [Fact]
    public async Task AcquisitionDialogNamesTheRowAndCancelDoesNotAcquire()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new ViewBrowserSource(
            BrowserUseChromiumState.BrowserNotInstalled,
            null,
            new BrowserUseManifestOffer("131.0.6778.0", "storage.googleapis.com", 42, BrowserUseCopy.LicenceLine));
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(async () =>
        {
            var page = new BrowserUseSettingsPage(source);
            await page.LoadAsync(ct).ConfigureAwait(true);
            var window = Show(page, out var view);
            try
            {
                view.Acquire.IsVisible.ShouldBeTrue();
                view.Acquire.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                SettingsTree.SettleSafe(window);
                var text = view.AcquisitionText.Text.ShouldNotBeNull();
                text.ShouldContain("131.0.6778.0");
                text.ShouldContain("storage.googleapis.com");
                text.ShouldContain("42");
                text.ShouldContain(BrowserUseCopy.LicenceLine);
                view.Cancel.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                source.AcquireCalls.ShouldBe(0);
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    private static Window Show(BrowserUseSettingsPage page, out BrowserUsePageView view)
    {
        view = new BrowserUsePageView { DataContext = page };
        var window = new Window { Width = 900, Height = 1400, Content = view };
        window.Show();
        SettingsTree.SettleSafe(window);
        return window;
    }

    private sealed class ViewBrowserSource : IBrowserUseSettingsSource
    {
        private readonly BrowserUseSnapshot _snapshot;

        public ViewBrowserSource(BrowserUseChromiumState state, string? version, BrowserUseManifestOffer? offer) =>
            _snapshot = new BrowserUseSnapshot(state, version, BrowserUseSettingsValues.Defaults, offer);

        public int AcquireCalls { get; private set; }

        public Task<BrowserUseSnapshot> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_snapshot);

        public Task SaveAsync(BrowserUseSettingsValues values, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AcquireAsync(CancellationToken cancellationToken)
        {
            AcquireCalls++;
            return Task.CompletedTask;
        }
    }
}
