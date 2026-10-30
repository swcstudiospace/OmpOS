#pragma warning disable CA2007
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

/// <summary>Proves desktop take-over, F6 release, the chrome indicator and Stop.</summary>
public sealed class TakeOverControllerTests
{
    /// <summary>Pointer and key events map through frame metadata, and agent calls wait during take-over.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task TakeOver_MapsInput_AndWaitsAgentCalls()
    {
        var session = new ScriptedInputSession();
        var controller = new TakeOverController(session);
        var metadata = new BrowserFrameMetadata(0, 2, 200, 100, 4, 8, 1);
        controller.NoteFrame(new BrowserFrame(new byte[] { 1 }, metadata, "1"));
        await controller.TakeOverAsync().ConfigureAwait(true);
        controller.KeyboardCaptured.ShouldBeTrue();
        controller.Gate.UserHasControl.ShouldBeTrue();
        var waiting = controller.Gate.WaitIfTakenOverAsync(CancellationToken.None);
        waiting.IsCompleted.ShouldBeFalse();

        var input = new BrowserPointerInput(10, 20, 100, 50, "mousePressed", "left");
        var css = TakeOverController.ToCssPixels(input, metadata);
        css.X.ShouldBe(10, 0.0001);
        css.Y.ShouldBe(20, 0.0001);
        await controller.SendPointerAsync(input).ConfigureAwait(true);
        var mouse = session.Commands.Single(command => command.Method == "Input.dispatchMouseEvent").Args.ShouldBeOfType<Dictionary<string, object>>();
        mouse["x"].ShouldBe(css.X);
        mouse["y"].ShouldBe(css.Y);

        await controller.SendKeyAsync(new BrowserKeyInput("h", "hi", "keyDown")).ConfigureAwait(true);
        session.Commands.ShouldContain(command => command.Method == "Input.dispatchKeyEvent");
        session.Commands.ShouldContain(command => command.Method == "Input.insertText");

        await controller.ReleaseControlAsync().ConfigureAwait(true);
        controller.KeyboardCaptured.ShouldBeFalse();
        await waiting.ConfigureAwait(true);
        controller.Gate.UserHasControl.ShouldBeFalse();
    }

    /// <summary>A remote head cannot send input, and no relay input type exists on the controller.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task RemoteViewer_HasNoInputPath()
    {
        var session = new ScriptedInputSession();
        var controller = new TakeOverController(session, localDesktop: false);
        await Should.ThrowAsync<InvalidOperationException>(() => controller.TakeOverAsync()).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidOperationException>(() => controller.SendPointerAsync(new BrowserPointerInput(1, 1, 10, 10, "mousePressed", "left"))).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidOperationException>(() => controller.SendKeyAsync(new BrowserKeyInput("a", null, "keyDown"))).ConfigureAwait(true);
        session.Commands.ShouldBeEmpty();
        typeof(TakeOverController).GetMethods().Any(method => method.Name.Contains("Remote", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>The indicator is chrome. Stop cancels the call token, revokes control and leaves the browser open.</summary>
    /// <returns>The task.</returns>
    [Fact]
    public async Task Stop_CancelsInFlightCall_AndRevokesWithoutClosing()
    {
        var session = new ScriptedInputSession();
        var controller = new TakeOverController(session);
        controller.Indicator.Sentence.ShouldBe("The agent is controlling the browser");
        controller.Indicator.ShowStop.ShouldBeTrue();
        controller.Indicator.ShowClose.ShouldBeTrue();
        controller.Indicator.InsidePageFrame.ShouldBeFalse();
        typeof(BrowserFrame).GetProperties().Any(property => property.Name.Contains("Indicator", StringComparison.Ordinal)).ShouldBeFalse();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(controller.Gate.Cancellation);
        var waiting = Task.Delay(Timeout.Infinite, linked.Token);
        await controller.StopAsync().ConfigureAwait(true);
        controller.Control.ShouldBe(BrowserControlState.Revoked);
        controller.KeyboardCaptured.ShouldBeFalse();
        controller.Indicator.ShowStop.ShouldBeFalse();
        await Should.ThrowAsync<TaskCanceledException>(() => waiting).ConfigureAwait(true);
        session.Commands.ShouldNotContain(command => command.Method == "Browser.close" || command.Method == "Page.close");

        await controller.CloseBrowserAsync().ConfigureAwait(true);
        session.Commands.Single().Method.ShouldBe("Browser.close");
    }

    private sealed class ScriptedInputSession : ICDPSession
    {
        private readonly List<(string Method, object? Args)> _commands = [];

        public List<(string Method, object? Args)> Commands => _commands;

        public ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;

        public string Id => "scripted";

        public string CloseReason => string.Empty;

        public bool Detached => false;

        public event EventHandler? Disconnected;

        public event EventHandler<SessionEventArgs>? SessionAttached;

        public event EventHandler<SessionEventArgs>? SessionDetached;

        public event EventHandler<MessageEventArgs>? MessageReceived;

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
            _ = Disconnected;
            _ = SessionAttached;
            _ = SessionDetached;
            _ = MessageReceived;
        }
    }
}
