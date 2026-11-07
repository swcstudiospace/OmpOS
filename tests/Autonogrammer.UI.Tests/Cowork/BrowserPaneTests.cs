using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Contracts.Cowork.Browser;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Cowork.Browser;
using Autonogrammer.UI.Components.Cowork;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Cowork;

/// <summary>The web pane shows the desktop-only sentence, never a frame, and keeps Stop in the toolbar.</summary>
public sealed class BrowserPaneTests : UiTestContext
{
    /// <summary>Every state sentence is rendered. The web head never draws a frame or an image.</summary>
    [Fact]
    public void EveryStateRendersItsSentenceAndNeverAFrame()
    {
        foreach (var state in Enum.GetValues<BrowserPaneState>())
        {
            var feed = new FakeFeed(state)
            {
                LatestFrame = new BrowserFrame(
                    new byte[] { 0xFF, 0xD8, 0xFF },
                    new BrowserFrameMetadata(0, 1, 10, 10, 0, 0, 0),
                    "Example|example.test"),
            };
            using var model = new BrowserPaneViewModel(feed, new FakeControl { Control = BrowserControlState.Agent });
            var cut = Render<BrowserPane>(p => p.Add(c => c.Model, model));
            var expected = state == BrowserPaneState.RemoteViewUnavailable
                ? BrowserPaneCopy.RemoteViewSentence
                : BrowserPaneCopy.Sentence(state);
            cut.Find(".autonogrammer-browser-pane-sentence").TextContent.ShouldBe(expected);
            cut.FindAll("img").ShouldBeEmpty();
            cut.FindAll("[data-frame]").ShouldBeEmpty();
            cut.Find("[role=region]").GetAttribute("aria-label").ShouldBe("Browser");
            cut.Find("[role=toolbar]").GetAttribute("aria-label").ShouldBe("Browser controls");
            cut.Find("[aria-live=polite]").ShouldNotBeNull();
            cut.Find(".autonogrammer-browser-pane-stop").TextContent.ShouldBe("Stop");
            cut.Find(".autonogrammer-browser-pane-indicator").TextContent.ShouldBe(BrowserChromeCopy.AgentControlling);
        }
    }

    /// <summary>Take over, Stop and Close are operable. Announcements coalesce to one per two seconds.</summary>
    [Fact]
    public async Task ControlsAreOperableAndAnnouncementsCoalesce()
    {
        var feed = new FakeFeed(BrowserPaneState.Live);
        var control = new FakeControl();
        using var model = new BrowserPaneViewModel(feed, control, new FakeTime());
        var cut = Render<BrowserPane>(p => p.Add(c => c.Model, model));
        await cut.Find("button").ClickAsync().ConfigureAwait(true);
        control.Taken.ShouldBe(1);
        await cut.Find(".autonogrammer-browser-pane-stop").ClickAsync().ConfigureAwait(true);
        control.Stopped.ShouldBe(1);
        feed.State = BrowserPaneState.UserControl;
        feed.Raise();
        feed.Raise();
        model.Announcements.ShouldBe(1);
        cut.Find("[aria-live=polite]").TextContent.ShouldBe(BrowserPaneCopy.Sentence(BrowserPaneState.UserControl));
    }

    /// <summary>ModelCannotSeeImages offers the vision-model action.</summary>
    [Fact]
    public void ModelCannotSeeImagesOffersTheVisionAction()
    {
        var feed = new FakeFeed(BrowserPaneState.ModelCannotSeeImages);
        using var model = new BrowserPaneViewModel(feed, new FakeControl());
        model.SetListedModels([new BrowserListedModel("vision-a", true)]);
        var cut = Render<BrowserPane>(p => p.Add(c => c.Model, model));
        cut.Markup.ShouldContain(BrowserPaneCopy.ChooseVisionModel);
        cut.Markup.ShouldContain("vision-a");
    }

    /// <summary>Reduced motion disables the single pane transition.</summary>
    [Fact]
    public void ReducedMotionDisablesTheSingleTransition()
    {
        var css = File.ReadAllText(Path.Combine(CssSources.StylesDirectory, "browser-pane.css"));
        css.ShouldContain("transition: inline-size var(--autonogrammer-duration)");
        css.ShouldContain("@media (prefers-reduced-motion: reduce)");
        css.ShouldContain("transition: none");
        css.ShouldNotContain("#");
    }

    /// <summary>The workbench dock mounts the pane and shows the remote sentence with no frame.</summary>
    [Fact]
    public void TheWorkbenchDockShowsTheRemoteSentence()
    {
        Services.AddSingleton<ICoworkAccess>(new FakeAccess());
        var feed = new FakeFeed(BrowserPaneState.RemoteViewUnavailable);
        using var model = new BrowserPaneViewModel(feed, new FakeControl());
        using var shell = new CoworkShellModel(new MemoryCatalog());
        shell.RegisterPanel(model);
        var cut = Render<CoworkWorkbench>(p => p.Add(c => c.Shell, shell));
        cut.Find("[aria-label=Dock]").TextContent.ShouldContain(BrowserPaneCopy.RemoteViewSentence);
        cut.FindAll("img").ShouldBeEmpty();
        cut.FindAll("[data-frame]").ShouldBeEmpty();
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

        public int Stopped { get; private set; }

        public Task TakeOverAsync(CancellationToken cancellationToken = default)
        {
            Taken++;
            KeyboardCaptured = true;
            return Task.CompletedTask;
        }

        public Task ReleaseControlAsync(CancellationToken cancellationToken = default)
        {
            KeyboardCaptured = false;
            return Task.CompletedTask;
        }

        public Task SendPointerAsync(BrowserPointerInput input, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendKeyAsync(BrowserKeyInput input, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Stopped++;
            Control = BrowserControlState.Revoked;
            return Task.CompletedTask;
        }

        public Task CloseBrowserAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class FakeAccess : ICoworkAccess
    {
        public event EventHandler? Changed { add { } remove { } }

        public bool IsResolved => true;

        public bool IsAvailable => true;

        public bool OffersSignIn => false;

        public bool LoadFailed => false;

        public Task RetryAsync() => Task.CompletedTask;
    }

    private sealed class MemoryCatalog : ICoworkSessionCatalog
    {
        public event EventHandler? Changed { add { } remove { } }

        public IReadOnlyList<CoworkSessionSummary> Snapshot() => [];
    }
}
