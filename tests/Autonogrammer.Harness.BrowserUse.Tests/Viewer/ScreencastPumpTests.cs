#pragma warning disable CA2007
#pragma warning disable MA0002
#pragma warning disable MA0006
#pragma warning disable VSTHRD003
#pragma warning disable xUnit1051
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Autonogrammer.Contracts.Cowork.Browser;
using Autonogrammer.Harness.BrowserUse.Viewer;
using PuppeteerSharp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Viewer;

/// <summary>Proves the pull-based screencast bounds and the pane-state catalogue.</summary>
public sealed class ScreencastPumpTests
{
    /// <summary>A burst keeps one frame in flight per viewer and drops the rest instead of queueing them.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task Burst_KeepsOneFrameInFlight_AndDropsTheRest()
    {
        var session = new ScriptedScreencastSession();
        var pump = new ScreencastPump();
        var delivered = 0;
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await pump.AddViewerAsync(session, async frame =>
        {
            _ = frame;
            delivered++;
            await hold.Task.ConfigureAwait(true);
        }, LocalBrowserPaneFeed.JpegQuality, LocalBrowserPaneFeed.LongEdge, sendLastFrame: true).ConfigureAwait(true);

        session.Commands.ShouldContain(command => command.Method == "Page.startScreencast");
        session.Emit(1);
        await WaitUntilAsync(() => delivered == 1).ConfigureAwait(true);
        session.InFlight.ShouldBe(1);
        session.Emit(2);
        session.Emit(3);
        session.Emit(4);
        await WaitUntilAsync(() => session.Acked.Contains(2) && session.Acked.Contains(3) && session.Acked.Contains(4)).ConfigureAwait(true);
        delivered.ShouldBe(1);
        session.InFlight.ShouldBe(1);
        session.Acked.ShouldNotContain(1);
        hold.SetResult();
        await WaitUntilAsync(() => session.Acked.Contains(1)).ConfigureAwait(true);
        session.InFlight.ShouldBe(0);
    }

    /// <summary>No viewer means stopScreencast, and a late viewer is started with sendLastFrame.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task NoViewer_Stops_AndLateViewer_ReceivesSendLastFrame()
    {
        var session = new ScriptedScreencastSession();
        var pump = new ScreencastPump();
        session.Commands.ShouldNotContain(command => command.Method == "Page.startScreencast");
        var delivered = 0;
        await pump.AddViewerAsync(session, frame =>
        {
            _ = frame;
            delivered++;
            return Task.CompletedTask;
        }, 70, 1280, sendLastFrame: true).ConfigureAwait(true);
        await pump.RemoveViewerAsync().ConfigureAwait(true);
        session.Commands.Last().Method.ShouldBe("Page.stopScreencast");
        pump.ViewerCount.ShouldBe(0);

        await pump.AddViewerAsync(session, frame =>
        {
            _ = frame;
            delivered++;
            return Task.CompletedTask;
        }, 70, 1280, sendLastFrame: true).ConfigureAwait(true);
        var start = session.Commands.Last(command => command.Method == "Page.startScreencast");
        var args = start.Args.ShouldBeOfType<Dictionary<string, object>>();
        args["sendLastFrame"].ShouldBe(true);
        args["format"].ShouldBe("jpeg");
        args["maxFramesInFlight"].ShouldBe(1);
        session.Emit(9);
        await WaitUntilAsync(() => delivered == 1).ConfigureAwait(true);
        delivered.ShouldBe(1);
    }

    /// <summary>The desktop feed asks for quality 70 and a 1280 long edge, and publishes at most 10 frames a second.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task LocalFeed_BoundsRateQualityAndLongEdge()
    {
        var session = new ScriptedScreencastSession();
        var clock = new ManualClock();
        var feed = new LocalBrowserPaneFeed(session, clock);
        await feed.AttachViewerAsync().ConfigureAwait(true);
        var args = session.Commands.Single(command => command.Method == "Page.startScreencast").Args.ShouldBeOfType<Dictionary<string, object>>();
        args["quality"].ShouldBe(LocalBrowserPaneFeed.JpegQuality);
        args["quality"].ShouldBe(70);
        args["maxWidth"].ShouldBe(LocalBrowserPaneFeed.LongEdge);
        args["maxHeight"].ShouldBe(1280);
        args["format"].ShouldBe("jpeg");
        LocalBrowserPaneFeed.FramesPerSecond.ShouldBe(10);

        session.Emit(1);
        await WaitUntilAsync(() => feed.LatestFrame?.SessionId == "1").ConfigureAwait(true);
        clock.Advance(TimeSpan.FromMilliseconds(50));
        session.Emit(2);
        await WaitUntilAsync(() => session.Acked.Contains(2)).ConfigureAwait(true);
        feed.LatestFrame!.SessionId.ShouldBe("1");
        clock.Advance(TimeSpan.FromMilliseconds(50));
        session.Emit(3);
        await WaitUntilAsync(() => feed.LatestFrame?.SessionId == "3").ConfigureAwait(true);
        await feed.DetachViewerAsync().ConfigureAwait(true);
        session.Commands.Last().Method.ShouldBe("Page.stopScreencast");
    }

    /// <summary>Every degraded state has a sentence and a fix, and the remote sentence is desktop-only.</summary>
    [Fact]
    public void BrowserPaneState_CoversEveryDegradedName()
    {
        string[] required =
        [
            "Idle", "Starting", "Live", "UserControl", "Stopped", "RemoteViewUnavailable",
            "BrowserDisabled", "BrowserNotInstalled", "ManagedBrowserNotPinned", "BrowserTooOld",
            "SandboxUnavailable", "ProfilePathUnsafe", "FontsMissing", "LaunchFailed", "Crashed",
            "UserHasControl", "ControlRevoked", "ModelCannotSeeImages", "ImageBudgetExhausted",
            "StaleRef", "OriginDenied", "BlockedByPolicy", "DownloadDenied", "PopupClosed",
            "DialogDismissed", "PageTooLarge", "NavigationTimeout", "RateLimited", "SessionLimitReached",
            "EgressGuardUnavailable", "NoLocalApprover",
        ];
        var names = Enum.GetNames<BrowserPaneState>();
        foreach (var name in required)
        {
            names.ShouldContain(name);
        }

        BrowserPaneCopy.Sentence(BrowserPaneState.RemoteViewUnavailable).ShouldBe(BrowserPaneCopy.RemoteViewSentence);
        BrowserPaneCopy.RemoteViewSentence.ShouldContain("This browser runs on your desktop");
        foreach (var state in Enum.GetValues<BrowserPaneState>())
        {
            BrowserPaneCopy.Sentence(state).ShouldNotBeNullOrWhiteSpace();
            BrowserPaneCopy.FixAction(state).ShouldNotBeNullOrWhiteSpace();
        }
    }

    /// <summary>The relay feed type is absent from src.</summary>
    [Fact]
    public void Src_HasNoRelayFeedType()
    {
        var banned = "Relay" + "BrowserPaneFeed";
        var root = FindRepoRoot();
        var hits = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Any(path => File.ReadAllText(path).Contains(banned, StringComparison.Ordinal));
        hits.ShouldBeFalse();
    }

    private static async Task WaitUntilAsync(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!ready() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10).ConfigureAwait(true);
        }

        ready().ShouldBeTrue();
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

    private sealed class ScriptedScreencastSession : ICDPSession
    {
        private readonly List<(string Method, object? Args)> _commands = [];
        private readonly List<int> _acked = [];
        private int _emitted;

        public List<(string Method, object? Args)> Commands => _commands;

        public List<int> Acked => _acked;

        public int InFlight => _emitted - _acked.Count;

        public ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;

        public string Id => "scripted";

        public string CloseReason => string.Empty;

        public bool Detached => false;

        public event EventHandler? Disconnected;

        public event EventHandler<SessionEventArgs>? SessionAttached;

        public event EventHandler<SessionEventArgs>? SessionDetached;

        public event EventHandler<MessageEventArgs>? MessageReceived;

        public void Emit(int sessionId)
        {
            _emitted++;
            var json = "{\"data\":\"" + Convert.ToBase64String([1, 2, 3]) + "\",\"sessionId\":" + sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"metadata\":{\"offsetTop\":0,\"pageScaleFactor\":1,\"deviceWidth\":1280,\"deviceHeight\":800,\"scrollOffsetX\":0,\"scrollOffsetY\":0,\"timestamp\":1}}";
            using var document = JsonDocument.Parse(json);
            var args = new MessageEventArgs();
            typeof(MessageEventArgs).GetProperty(nameof(MessageEventArgs.MessageID))!.SetValue(args, "Page.screencastFrame");
            typeof(MessageEventArgs).GetProperty(nameof(MessageEventArgs.MessageData))!.SetValue(args, document.RootElement.Clone());
            MessageReceived?.Invoke(this, args);
            _ = Disconnected;
            _ = SessionAttached;
            _ = SessionDetached;
        }

        public Task DetachAsync() => Task.CompletedTask;

        public void Close(string closeReason) => _ = closeReason;

        public Task<T> SendAsync<T>(string method, object? args = null, CommandOptions? options = null)
        {
            _ = options;
            Record(method, args);
            return Task.FromResult<T>(default!);
        }

        public Task<JsonElement?> SendAsync(string method, object? args = null, bool waitForCallback = true, CommandOptions? options = null)
        {
            _ = waitForCallback;
            _ = options;
            Record(method, args);
            return Task.FromResult<JsonElement?>(null);
        }

        private void Record(string method, object? args)
        {
            _commands.Add((method, args));
            if (!string.Equals(method, "Page.screencastFrameAck", StringComparison.Ordinal) || args is not Dictionary<string, object> values)
            {
                return;
            }

            _acked.Add((int)values["sessionId"]);
        }
    }
}
