#pragma warning disable CA2000
#pragma warning disable CA2007
#pragma warning disable MA0002
#pragma warning disable MA0006
#pragma warning disable VSTHRD003
#pragma warning disable xUnit1051
using Autonogrammer.Contracts.Cowork.Browser;
using Autonogrammer.Presentation.Cowork.Browser;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cowork.Browser;

/// <summary>Proves the framework-free pane model.</summary>
public sealed class BrowserPaneViewModelTests
{
    /// <summary>Every state renders one sentence and one fix action.</summary>
    [Fact]
    public void EveryState_RendersSentenceAndFixAction()
    {
        var feed = new FakeFeed();
        var control = new FakeControl();
        var model = new BrowserPaneViewModel(feed, control);
        foreach (var state in Enum.GetValues<BrowserPaneState>())
        {
            feed.Set(state, null);
            model.Sentence.ShouldBe(BrowserPaneCopy.Sentence(state));
            model.FixAction.ShouldBe(BrowserPaneCopy.FixAction(state));
            model.Sentence.ShouldNotBeNullOrWhiteSpace();
            model.FixAction.ShouldNotBeNullOrWhiteSpace();
        }

        feed.Set(BrowserPaneState.RemoteViewUnavailable, null);
        model.Sentence.ShouldContain("This browser runs on your desktop");
        feed.Set(BrowserPaneState.ModelCannotSeeImages, null);
        model.FixAction.ShouldBe("Choose a vision model");
    }

    /// <summary>The vision fix lists catalogue rows that already accept images, and adds no catalogue flag.</summary>
    [Fact]
    public void ModelCannotSeeImages_ListsExistingVisionModels()
    {
        var feed = new FakeFeed();
        var model = new BrowserPaneViewModel(feed, new FakeControl());
        model.SetListedModels(
        [
            new BrowserListedModel("text-only", false),
            new BrowserListedModel("vision", true),
        ]);
        feed.Set(BrowserPaneState.ModelCannotSeeImages, null);
        model.FixAction.ShouldBe(BrowserPaneCopy.ChooseVisionModel);
        model.VisionSelectors.ShouldBe(["vision"]);
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Autonogrammer.Presentation", "Cowork", "Browser", "BrowserPaneViewModel.cs"));
        source.ShouldContain("OmpModelInfo.AcceptsImages");
        source.ShouldContain("CoworkSessionContext.ModelReadsImages");
        source.ShouldNotContain("Avalonia");
        source.ShouldNotContain("Microsoft.AspNetCore.Components");
    }

    /// <summary>The Agent strip reads size, format, byte count and time from returned bytes.</summary>
    [Fact]
    public void AgentStrip_ReadsReturnedImageBytes()
    {
        var clock = new ManualClock();
        var model = new BrowserPaneViewModel(new FakeFeed(), new FakeControl(), clock);
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x02, 0x00, 0x03 };
        model.ShowReturnedImage(bytes);
        model.LastImageByteCount.ShouldBe(bytes.Length);
        model.LastImageFormat.ShouldBe("jpeg");
        model.LastImageWidth.ShouldBe(3);
        model.LastImageHeight.ShouldBe(2);
        model.LastImageTime.ShouldBe(clock.Utc);
    }

    /// <summary>Announcements coalesce to one per two seconds.</summary>
    [Fact]
    public void Announcements_CoalesceToOnePerTwoSeconds()
    {
        var clock = new ManualClock();
        var feed = new FakeFeed();
        var model = new BrowserPaneViewModel(feed, new FakeControl(), clock);
        feed.Set(BrowserPaneState.Live, null);
        model.Announcements.ShouldBe(1);
        clock.Advance(TimeSpan.FromSeconds(1));
        feed.Set(BrowserPaneState.UserControl, null);
        model.Announcements.ShouldBe(1);
        model.Announcement.ShouldBe(BrowserPaneCopy.Sentence(BrowserPaneState.Live));
        clock.Advance(TimeSpan.FromSeconds(1));
        feed.Set(BrowserPaneState.Stopped, null);
        model.Announcements.ShouldBe(2);
        model.Announcement.ShouldBe(BrowserPaneCopy.Sentence(BrowserPaneState.Stopped));
    }

    /// <summary>Take over, Release, Stop and Close, plus pointer and key events, reach the control.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task CommandsAndInput_ReachTheControl()
    {
        var control = new FakeControl();
        var model = new BrowserPaneViewModel(new FakeFeed(), control);
        await model.InvokeCommandAsync("Take over").ConfigureAwait(true);
        await model.InvokeCommandAsync("Release").ConfigureAwait(true);
        await model.InvokeCommandAsync("Stop").ConfigureAwait(true);
        await model.InvokeCommandAsync("Close").ConfigureAwait(true);
        var pointer = new BrowserPointerInput(1, 2, 3, 4, "mousePressed", "left");
        var key = new BrowserKeyInput("a", null, "keyDown");
        await model.SendPointerAsync(pointer).ConfigureAwait(true);
        await model.SendKeyAsync(key).ConfigureAwait(true);
        control.Calls.ShouldBe(["Take over", "Release", "Stop", "Close", "Pointer", "Key"]);
        control.Pointer.ShouldBe(pointer);
        control.Key.ShouldBe(key);
        BrowserPaneViewModel.Commands.Single(command => command.Name == "Release").AccessKey.ShouldBe("F6");
        model.HeaderSentence.ShouldBe(BrowserChromeCopy.AgentControlling);
        control.ControlState = BrowserControlState.Revoked;
        await model.StopAsync().ConfigureAwait(true);
        model.HeaderOffersStop.ShouldBeFalse();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Autonogrammer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Autonogrammer.slnx was not found.");
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Utc { get; private set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Utc;

        public void Advance(TimeSpan by) => Utc += by;
    }

    private sealed class FakeFeed : IBrowserPaneFeed
    {
        public BrowserPaneState State { get; private set; } = BrowserPaneState.Idle;

        public BrowserFrame? LatestFrame { get; private set; }

        public event EventHandler? Changed;

        public void Set(BrowserPaneState state, BrowserFrame? frame)
        {
            State = state;
            LatestFrame = frame;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeControl : IBrowserPaneControl
    {
        public List<string> Calls { get; } = [];

        public BrowserControlState ControlState { get; set; } = BrowserControlState.Agent;

        public BrowserPointerInput? Pointer { get; private set; }

        public BrowserKeyInput? Key { get; private set; }

        public BrowserControlState Control => ControlState;

        public bool KeyboardCaptured { get; private set; }

        public Task TakeOverAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Take over");
            KeyboardCaptured = true;
            return Task.CompletedTask;
        }

        public Task ReleaseControlAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Release");
            KeyboardCaptured = false;
            return Task.CompletedTask;
        }

        public Task SendPointerAsync(BrowserPointerInput input, CancellationToken cancellationToken = default)
        {
            Calls.Add("Pointer");
            Pointer = input;
            return Task.CompletedTask;
        }

        public Task SendKeyAsync(BrowserKeyInput input, CancellationToken cancellationToken = default)
        {
            Calls.Add("Key");
            Key = input;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Stop");
            return Task.CompletedTask;
        }

        public Task CloseBrowserAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Close");
            return Task.CompletedTask;
        }
    }
}
