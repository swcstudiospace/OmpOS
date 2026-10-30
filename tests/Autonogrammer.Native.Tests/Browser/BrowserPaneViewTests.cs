using Autonogrammer.Contracts.Cowork.Browser;
using Autonogrammer.Native.Views.Browser;
using Autonogrammer.Presentation.Cowork.Browser;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Browser;

/// <summary>Headless pane: every state sentence, chrome Stop outside the frame, and F6 leaving capture.</summary>
public sealed class BrowserPaneViewTests
{
    /// <summary>Every pane state renders its sentence. A remote feed renders the desktop-only sentence and no frame.</summary>
    [Fact]
    public async Task EveryStateRendersItsSentence()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            foreach (var state in Enum.GetValues<BrowserPaneState>())
            {
                var feed = new FakeFeed(state);
                using var model = new BrowserPaneViewModel(feed, new FakeControl());
                var panel = new BrowserPanePanel(model);
                panel.Title.ShouldBe("Browser");
                panel.Id.ShouldBe("browser");
                panel.Content.ShouldBe(model);
                var window = new Window { Content = panel.View, Width = 480, Height = 320 };
                try
                {
                    window.Show();
                    ShellHarness.Settle(window);
                    panel.View.SentenceText.Text.ShouldBe(BrowserPaneCopy.Sentence(state));
                    if (state == BrowserPaneState.RemoteViewUnavailable)
                    {
                        panel.View.FramePicture.IsVisible.ShouldBeFalse();
                    }
                }
                finally
                {
                    window.Close();
                }
            }

            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }

    /// <summary>The frame alt text carries the page title and the host. Stop sits outside the frame. F6 releases capture.</summary>
    [Fact]
    public async Task ChromeStopSitsOutsideTheFrameAndF6ReleasesCapture()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var feed = new FakeFeed(BrowserPaneState.Live)
            {
                LatestFrame = new BrowserFrame(
                    new byte[] { 0xFF, 0xD8, 0xFF },
                    new BrowserFrameMetadata(0, 1, 100, 80, 0, 0, 0),
                    "Example|example.test"),
            };
            var control = new FakeControl { Control = BrowserControlState.Agent };
            using var model = new BrowserPaneViewModel(feed, control, new FakeTime());
            var panel = new BrowserPanePanel(model);
            var window = new Window { Content = panel.View, Width = 480, Height = 320 };
            try
            {
                window.Show();
                ShellHarness.Settle(window);
                panel.View.HeaderText.IsVisible.ShouldBeTrue();
                panel.View.Frame.GetVisualDescendants().ShouldNotContain(panel.View.Stop);
                panel.View.Stop.IsTabStop.ShouldBeTrue();
                panel.View.TakeOver.IsTabStop.ShouldBeTrue();
                panel.View.CloseBrowser.IsTabStop.ShouldBeTrue();
                AutomationProperties.GetName(panel.View.FramePicture).ShouldBe("Example at example.test");

                panel.View.TakeOver.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                control.Taken.ShouldBe(1);
                panel.View.KeyboardCaptured.ShouldBeTrue();
                panel.View.Focus();
                window.KeyPress(Key.F6, default, PhysicalKey.F6, null);
                ShellHarness.Settle(window);
                control.Released.ShouldBe(1);
                panel.View.KeyboardCaptured.ShouldBeFalse();

                feed.State = BrowserPaneState.UserControl;
                feed.Raise();
                feed.Raise();
                ShellHarness.Settle(window);
                model.Announcements.ShouldBe(1);
                panel.View.AnnouncementText.Text.ShouldBe(BrowserPaneCopy.Sentence(BrowserPaneState.UserControl));

                panel.View.Stop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                control.Stopped.ShouldBe(1);
                await Task.CompletedTask.ConfigureAwait(true);
            }
            finally
            {
                window.Close();
            }
        }, token).ConfigureAwait(true);
    }

    /// <summary>ModelCannotSeeImages renders the choose-a-vision-model action.</summary>
    [Fact]
    public async Task ModelCannotSeeImagesOffersTheVisionAction()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var feed = new FakeFeed(BrowserPaneState.ModelCannotSeeImages);
            using var model = new BrowserPaneViewModel(feed, new FakeControl());
            model.SetListedModels([new BrowserListedModel("vision-a", true), new BrowserListedModel("text-only", false)]);
            var view = new BrowserPaneView();
            view.Attach(model);
            var window = new Window { Content = view, Width = 480, Height = 320 };
            try
            {
                window.Show();
                ShellHarness.Settle(window);
                view.Fix.IsVisible.ShouldBeTrue();
                view.Fix.Content.ShouldBe(BrowserPaneCopy.ChooseVisionModel);
                view.Vision.ItemsSource.Cast<string>().ShouldBe(["vision-a"]);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }

    /// <summary>Native.Views and UI do not reference Harness.BrowserUse.</summary>
    [Fact]
    public void ViewsDoNotReferenceHarnessBrowserUse()
    {
        var root = FindRepoRoot();
        foreach (var relative in new[] { "src/Autonogrammer.Native.Views", "src/Autonogrammer.UI" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, relative), "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                File.ReadAllText(file).ShouldNotContain("Harness.BrowserUse", Case.Sensitive, file);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Autonogrammer.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repo root was not found.");
    }

    private sealed class FakeFeed(BrowserPaneState state) : IBrowserPaneFeed
    {
        public BrowserPaneState State { get; set; } = state;

        public BrowserFrame? LatestFrame { get; set; }

        public event EventHandler? Changed;

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeControl : IBrowserPaneControl
    {
        public BrowserControlState Control { get; set; } = BrowserControlState.User;

        public bool KeyboardCaptured { get; private set; }

        public int Taken { get; private set; }

        public int Released { get; private set; }

        public int Stopped { get; private set; }

        public Task TakeOverAsync(CancellationToken cancellationToken = default)
        {
            Taken++;
            KeyboardCaptured = true;
            Control = BrowserControlState.User;
            return Task.CompletedTask;
        }

        public Task ReleaseControlAsync(CancellationToken cancellationToken = default)
        {
            Released++;
            KeyboardCaptured = false;
            Control = BrowserControlState.Agent;
            return Task.CompletedTask;
        }

        public Task SendPointerAsync(BrowserPointerInput input, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendKeyAsync(BrowserKeyInput input, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Stopped++;
            Control = BrowserControlState.Revoked;
            KeyboardCaptured = false;
            return Task.CompletedTask;
        }

        public Task CloseBrowserAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }
}
