using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.BrowserUse;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The acquisition dialog shows a refused download under its text, and Confirm is off while a download runs.</summary>
public sealed class BrowserUseConfirmViewTests
{
    private const string Refusal = "The archive checksum did not match.";

    [Fact]
    public async Task ARefusedDownloadIsShownAndConfirmIsOfferedAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new ScriptedBrowserSource(_ => throw new SettingsSourceException(Refusal));
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(async () =>
        {
            var page = new BrowserUseSettingsPage(source);
            await page.LoadAsync(ct).ConfigureAwait(true);
            var (window, view) = Show(page);
            try
            {
                view.Acquire.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                view.Confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                SettingsTree.SettleSafe(window);

                source.AcquireCalls.ShouldBe(1);
                view.AcquisitionErrorLine.IsVisible.ShouldBeTrue();
                view.AcquisitionErrorLine.Text.ShouldBe(Refusal);
                view.Confirm.IsEnabled.ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task ConfirmIsOffWhileADownloadRunsAndAFurtherConfirmStartsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource();
        var source = new ScriptedBrowserSource(token => gate.Task.WaitAsync(token));
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(async () =>
        {
            var page = new BrowserUseSettingsPage(source);
            await page.LoadAsync(ct).ConfigureAwait(true);
            var (window, view) = Show(page);
            try
            {
                view.Acquire.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                view.Confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                SettingsTree.SettleSafe(window);

                view.Confirm.IsEnabled.ShouldBeFalse();
                view.Confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                source.AcquireCalls.ShouldBe(1);

                gate.SetResult();
                for (var attempt = 0; attempt < 100 && !view.Confirm.IsEnabled; attempt++)
                {
                    SettingsTree.SettleSafe(window);
                    await Task.Delay(10, ct).ConfigureAwait(true);
                }

                view.Confirm.IsEnabled.ShouldBeTrue();
                view.AcquisitionErrorLine.IsVisible.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    private static (Window Window, BrowserUsePageView View) Show(BrowserUseSettingsPage page)
    {
        var view = new BrowserUsePageView { DataContext = page };
        var window = new Window { Width = 900, Height = 1400, Content = view };
        window.Show();
        SettingsTree.SettleSafe(window);
        return (window, view);
    }

    private sealed class ScriptedBrowserSource : IBrowserUseSettingsSource
    {
        private readonly Func<CancellationToken, Task> _acquire;
        private readonly BrowserUseSnapshot _snapshot = new(
            BrowserUseChromiumState.BrowserNotInstalled,
            null,
            BrowserUseSettingsValues.Defaults,
            new BrowserUseManifestOffer("131.0.6778.0", "storage.googleapis.com", 42, BrowserUseCopy.LicenceLine));

        public ScriptedBrowserSource(Func<CancellationToken, Task> acquire) => _acquire = acquire;

        public int AcquireCalls { get; private set; }

        public Task<BrowserUseSnapshot> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_snapshot);

        public Task SaveAsync(BrowserUseSettingsValues values, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AcquireAsync(CancellationToken cancellationToken)
        {
            AcquireCalls++;
            return _acquire(cancellationToken);
        }
    }
}
