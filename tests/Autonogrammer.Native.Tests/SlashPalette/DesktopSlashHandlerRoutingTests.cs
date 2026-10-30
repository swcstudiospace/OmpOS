using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Commands.Handlers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.SlashPalette;

/// <summary>How the desktop host routes an action between the slash handlers of the session and the plain callbacks of the head.</summary>
public sealed class DesktopSlashHandlerRoutingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SlashActionRequest Request(string actionId, string typed = "") => new(actionId, string.Empty, typed);

    /// <summary>A handler runs its action and is the only thing that decides whether the action can run: the callback of the same action is never reached.</summary>
    [Fact]
    public async Task AHandlerWinsOverTheCallbackOfTheSameAction()
    {
        var callbackRuns = 0;
        var handler = new ScriptedHandler(SlashActionIds.SideOpen, canRun: true, SlashActionResult.Completed("handled"));
        var host = new DesktopSlashActionHost(new DesktopSlashTargets
        {
            OpenSideChat = (_, _) =>
            {
                callbackRuns++;
                return Task.CompletedTask;
            },
            Handlers = [handler],
        });

        var result = await host.RunAsync(Request(SlashActionIds.SideOpen, "why?"), Token).ConfigureAwait(true);

        result.ShouldBe(SlashActionResult.Completed("handled"));
        handler.Requests.ShouldBe([Request(SlashActionIds.SideOpen, "why?")]);
        callbackRuns.ShouldBe(0);
    }

    /// <summary>A handler that cannot run keeps its action disabled even when a callback exists, and says the command is not ready.</summary>
    [Fact]
    public async Task AHandlerThatCannotRunDisablesItsAction()
    {
        var host = new DesktopSlashActionHost(new DesktopSlashTargets
        {
            OpenSideChat = (_, _) => Task.CompletedTask,
            Handlers = [new ScriptedHandler(SlashActionIds.SideOpen, canRun: false, SlashActionResult.Completed("never"))],
        });

        host.CanRun(SlashActionIds.SideOpen).ShouldBeFalse();
        var result = await host.RunAsync(Request(SlashActionIds.SideOpen), Token).ConfigureAwait(true);

        result.ShouldBe(SlashActionResult.NotAvailable(HandlerCopy.NotReady));
    }

    /// <summary>An action no handler names still runs through the callbacks of the head, and an action nobody names says the window cannot run it.</summary>
    [Fact]
    public async Task OtherActionsFallBackToTheCallbacks()
    {
        var started = 0;
        var host = new DesktopSlashActionHost(new DesktopSlashTargets
        {
            NewChat = _ =>
            {
                started++;
                return Task.CompletedTask;
            },
            Handlers = [new ScriptedHandler(HandlerActionIds.AppAbout, canRun: true, SlashActionResult.Completed("about"))],
        });

        host.CanRun(SlashActionIds.ChatNew).ShouldBeTrue();
        host.CanRun(HandlerActionIds.AppAbout).ShouldBeTrue();
        host.CanRun(HandlerActionIds.ChatFork).ShouldBeFalse();
        (await host.RunAsync(Request(SlashActionIds.ChatNew), Token).ConfigureAwait(true)).Outcome.ShouldBe(SlashActionOutcome.Completed);
        started.ShouldBe(1);
        (await host.RunAsync(Request(HandlerActionIds.ChatFork), Token).ConfigureAwait(true)).ShouldBe(SlashActionResult.NotAvailable(SlashPaletteCopy.ActionNotInThisWindow));
    }

    /// <summary>The cloud hand-off runs through its callback with the typed task, and cannot run without one.</summary>
    [Fact]
    public async Task TheCloudHandoffRunsThroughItsCallback()
    {
        string? task = null;
        var none = new DesktopSlashActionHost(new DesktopSlashTargets());
        var host = new DesktopSlashActionHost(new DesktopSlashTargets
        {
            HandOffToCloud = (typed, _) =>
            {
                task = typed;
                return Task.FromResult(SlashActionResult.Completed("handed off"));
            },
        });

        none.CanRun(SlashActionIds.CloudHandoff).ShouldBeFalse();
        host.CanRun(SlashActionIds.CloudHandoff).ShouldBeTrue();
        var result = await host.RunAsync(Request(SlashActionIds.CloudHandoff, "fix the login"), Token).ConfigureAwait(true);

        result.ShouldBe(SlashActionResult.Completed("handed off"));
        task.ShouldBe("fix the login");
    }

    /// <summary>A handler that throws is a failed command with the message, and a cancelled one is not swallowed.</summary>
    [Fact]
    public async Task AThrowingHandlerFailsTheCommandAndCancellationPasses()
    {
        var host = new DesktopSlashActionHost(new DesktopSlashTargets
        {
            Handlers =
            [
                new ThrowingHandler(HandlerActionIds.AppAbout, new InvalidOperationException("no window")),
                new ThrowingHandler(HandlerActionIds.AppQuit, new OperationCanceledException()),
            ],
        });

        var failed = await host.RunAsync(Request(HandlerActionIds.AppAbout), Token).ConfigureAwait(true);

        failed.ShouldBe(SlashActionResult.Failed("The command failed: no window"));
        await Should.ThrowAsync<OperationCanceledException>(() => host.RunAsync(Request(HandlerActionIds.AppQuit), Token)).ConfigureAwait(true);
    }

    /// <summary>Two handlers for the same action are a programming error the host refuses at construction.</summary>
    [Fact]
    public void TwoHandlersForOneActionAreRefused()
    {
        var handlers = new ISlashActionHandler[]
        {
            new ScriptedHandler(HandlerActionIds.AppAbout, canRun: true, SlashActionResult.Completed("a")),
            new ScriptedHandler(HandlerActionIds.AppAbout, canRun: true, SlashActionResult.Completed("b")),
        };

        Should.Throw<ArgumentException>(() => new DesktopSlashActionHost(new DesktopSlashTargets { Handlers = handlers }));
    }

    private sealed class ScriptedHandler(string actionId, bool canRun, SlashActionResult result) : ISlashActionHandler
    {
        public List<SlashActionRequest> Requests { get; } = [];

        public string ActionId => actionId;

        public bool CanRun => canRun;

        public Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingHandler(string actionId, Exception failure) : ISlashActionHandler
    {
        public string ActionId => actionId;

        public bool CanRun => true;

        public Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken) => throw failure;
    }
}
