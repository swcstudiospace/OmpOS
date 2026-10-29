using Autonogrammer.Presentation.Kanban;
using Autonogrammer.Presentation.Terminal;
using Autonogrammer.UI.Components.Terminal;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The omp terminal view: output reaches the screen in sequence exactly once, a dropped connection resumes after the last chunk, every state says what it is, and the composer only steers (PX-09).</summary>
public sealed class TerminalViewTests : UiTestContext
{
    private const string TerminalModule = "./_content/Autonogrammer.UI/scripts/autonogrammer-terminal.js";
    private const string SessionId = "term-9";

    /// <summary>Without a transport in the head the view is unavailable, gives the web head's reason and never loads the screen.</summary>
    [Fact]
    public void WithoutATransportTheViewIsUnavailableWithTheWebReason()
    {
        var module = JSInterop.SetupModule(TerminalModule);

        var cut = RenderTerminal();

        cut.Find(".autonogrammer-terminal").GetAttribute("data-state").ShouldBe("unavailable");
        cut.Find("h2").TextContent.ShouldBe(TerminalCopy.UnavailableHeading);
        cut.Find(".autonogrammer-terminal-unavailable-reason").TextContent.ShouldBe(TerminalAvailability.WebReason);
        cut.FindAll(".autonogrammer-terminal-screen").Count.ShouldBe(0);
        module.Invocations.Count.ShouldBe(0, "no screen is created for an unavailable terminal");
    }

    /// <summary>Chunks are written to the screen in sequence, the view goes Live with its token, and the screen is a named group in screen-reader order.</summary>
    [Fact]
    public void ChunksAreWrittenInSequenceAndTheViewGoesLive()
    {
        var module = SetupScreen();
        var transport = AddTransport((_, _, token) => ScriptedTerminalTransport.StreamAsync([(1, "one "), (2, "two "), (3, "three")], hold: true, cancellationToken: token));

        var cut = RenderTerminal();

        WaitForState(cut, "live");
        WrittenText(module).ShouldBe(["one ", "two ", "three"]);
        module.Invocations["write"].Select(static call => (int)call.Arguments[0]!).ShouldAllBe(token => token == ScreenToken);
        cut.Find(".autonogrammer-terminal-status-text").TextContent.ShouldBe(TerminalCopy.Connected);
        cut.Find(".autonogrammer-terminal-screen").GetAttribute("aria-label").ShouldBe(TerminalCopy.ScreenName);
        transport.Attaches.ShouldBe([(SessionId, 0L)]);
    }

    /// <summary>An at-least-once transport may replay chunks; the screen never prints one twice.</summary>
    [Fact]
    public void ReplayedChunksAreWrittenOnlyOnce()
    {
        var module = SetupScreen();
        AddTransport((_, _, token) => ScriptedTerminalTransport.StreamAsync([(1, "a"), (2, "b"), (2, "b"), (1, "a"), (3, "c")], hold: true, cancellationToken: token));

        var cut = RenderTerminal();

        WaitForState(cut, "live");
        cut.WaitForAssertion(() => WrittenText(module).ShouldBe(["a", "b", "c"]));
    }

    /// <summary>A chunk that skips ahead loses output, so the view attaches again after the last chunk it wrote and the missing output arrives.</summary>
    [Fact]
    public void AGapReattachesAfterTheLastAcceptedChunk()
    {
        var module = SetupScreen();
        var transport = AddTransport((attempt, _, token) => attempt == 1
            ? ScriptedTerminalTransport.StreamAsync([(1, "a"), (3, "c")], cancellationToken: token)
            : ScriptedTerminalTransport.StreamAsync([(2, "b"), (3, "c")], hold: true, cancellationToken: token));

        var cut = RenderTerminal();

        cut.WaitForAssertion(() => WrittenText(module).ShouldBe(["a", "b", "c"]));
        WaitForState(cut, "live");
        transport.Attaches.ShouldBe([(SessionId, 0L), (SessionId, 1L)]);
    }

    /// <summary>A broken connection shows Reconnecting, resumes after the last accepted chunk, drops the replayed one and goes Live again.</summary>
    [Fact]
    public void ABrokenConnectionReconnectsAfterTheLastAcceptedChunk()
    {
        var module = SetupScreen();
        var transport = AddTransport((attempt, _, token) => attempt == 1
            ? ScriptedTerminalTransport.StreamAsync([(1, "a")], thenThrow: new IOException("connection reset"), cancellationToken: token)
            : ScriptedTerminalTransport.StreamAsync([(1, "a"), (2, "b")], hold: true, cancellationToken: token));

        var cut = RenderTerminal();

        cut.WaitForAssertion(() => WrittenText(module).ShouldBe(["a", "b"]));
        WaitForState(cut, "live");
        transport.Attaches.ShouldBe([(SessionId, 0L), (SessionId, 1L)]);
    }

    /// <summary>While the connection is down the status line says it is reconnecting.</summary>
    [Fact]
    public void ReconnectingIsShownWhileTheConnectionIsDown()
    {
        SetupScreen();
        AddTransport((_, _, token) => ScriptedTerminalTransport.StreamAsync([], thenThrow: new IOException("connection reset"), cancellationToken: token));

        var cut = RenderTerminal(TimeSpan.FromHours(1));

        WaitForState(cut, "reconnecting");
        cut.Find(".autonogrammer-terminal-status-text").TextContent.ShouldBe(TerminalCopy.Reconnecting);
        cut.Find(".autonogrammer-terminal-status-text").GetAttribute("aria-live").ShouldBe("polite");
    }

    /// <summary>After five failed reconnects in a row the view stops, says the connection was lost and offers Reconnect, which attaches again.</summary>
    [Fact]
    public void GivingUpOffersReconnectWhichAttachesAgain()
    {
        SetupScreen();
        var transport = AddTransport((attempt, _, token) => attempt <= 6
            ? ScriptedTerminalTransport.StreamAsync([], thenThrow: new IOException("connection reset"), cancellationToken: token)
            : ScriptedTerminalTransport.StreamAsync([(1, "back")], hold: true, cancellationToken: token));

        var cut = RenderTerminal();

        WaitForState(cut, "ended");
        cut.Find(".autonogrammer-terminal-status-text").TextContent.ShouldBe(TerminalCopy.ConnectionLost);
        transport.Attaches.Count.ShouldBe(6, "the first attach and five retries");

        cut.Find("button[aria-label='" + TerminalCopy.ReconnectName + "']").Click();

        WaitForState(cut, "live");
        transport.Attaches.Count.ShouldBe(7);
        cut.FindAll("button[aria-label='" + TerminalCopy.ReconnectName + "']").Count.ShouldBe(0);
    }

    /// <summary>A stream that completes normally is a finished terminal: no reconnect, no Reconnect button.</summary>
    [Fact]
    public void ACompletedStreamEndsTheTerminal()
    {
        var module = SetupScreen();
        var transport = AddTransport((_, _, token) => ScriptedTerminalTransport.StreamAsync([(1, "bye")], cancellationToken: token));

        var cut = RenderTerminal();

        WaitForState(cut, "ended");
        WrittenText(module).ShouldBe(["bye"]);
        cut.Find(".autonogrammer-terminal-status-text").TextContent.ShouldBe(TerminalCopy.Ended);
        cut.FindAll("button[aria-label='" + TerminalCopy.ReconnectName + "']").Count.ShouldBe(0);
        transport.Attaches.Count.ShouldBe(1);
    }

    /// <summary>A transport that has no terminal for this head says so, and the view shows that reason instead of retrying.</summary>
    [Fact]
    public void ATransportThatCannotAttachShowsItsOwnReason()
    {
        SetupScreen();
        var transport = AddTransport((_, _, token) => ScriptedTerminalTransport.StreamAsync([], thenThrow: new NotSupportedException("The omp API has no PTY for this session."), cancellationToken: token));

        var cut = RenderTerminal();

        WaitForState(cut, "unavailable");
        cut.Find(".autonogrammer-terminal-unavailable-reason").TextContent.ShouldBe("The omp API has no PTY for this session.");
        transport.Attaches.Count.ShouldBe(1);
    }

    /// <summary>The composer only steers: Send now and Follow up go to the steering service with the session and the mode, an accepted message clears the box, and the result is announced.</summary>
    [Fact]
    public void ComposerSteersWithTheChosenMode()
    {
        SetupScreen();
        AddTransport((_, _, token) => ScriptedTerminalTransport.StreamAsync([], hold: true, cancellationToken: token));
        var steering = new WorkspaceCardSteering();
        Services.AddSingleton<ICardSteering>(steering);
        var cut = RenderTerminal();
        var group = cut.Find("[role='group'][aria-label='" + TerminalCopy.SteerName + "']");
        group.QuerySelector("button[aria-label='" + TerminalCopy.SendNowName + "']")!.HasAttribute("disabled").ShouldBeTrue("nothing is typed yet");
        group.QuerySelector("textarea")!.GetAttribute("maxlength").ShouldBe(TerminalCopy.MaxSteerChars.ToString(System.Globalization.CultureInfo.InvariantCulture));

        cut.Find("textarea").Input("Run the tests");
        cut.Find("button[aria-label='" + TerminalCopy.SendNowName + "']").Click();

        cut.WaitForAssertion(() => cut.Find(".autonogrammer-terminal-steer-result").TextContent.ShouldBe("Sent to the session"));
        cut.Find("textarea").GetAttribute("value").ShouldBeNullOrEmpty();

        cut.Find("textarea").Input("Then lint");
        cut.Find("button[aria-label='" + TerminalCopy.FollowUpName + "']").Click();

        cut.WaitForAssertion(() => steering.Calls.Count.ShouldBe(2));
        steering.Calls.ShouldBe([(SessionId, "Run the tests", CardSteerMode.SendNow), (SessionId, "Then lint", CardSteerMode.FollowUp)]);
    }

    /// <summary>A refused message stays in the box and the refusal is shown as an error.</summary>
    [Fact]
    public void RefusedSteeringKeepsTheTextAndShowsTheError()
    {
        SetupScreen();
        AddTransport((_, _, token) => ScriptedTerminalTransport.StreamAsync([], hold: true, cancellationToken: token));
        Services.AddSingleton<ICardSteering>(new WorkspaceCardSteering { Result = CardSteerResult.Failed("The session ended") });
        var cut = RenderTerminal();
        cut.Find("textarea").Input("Stop");

        cut.Find("button[aria-label='" + TerminalCopy.SendNowName + "']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".autonogrammer-terminal-steer-result").TextContent.ShouldBe("The session ended");
            cut.Find(".autonogrammer-terminal-steer-result").GetAttribute("data-failed").ShouldBe("true");
        });
        cut.Find("textarea").GetAttribute("value").ShouldBe("Stop");
    }

    /// <summary>Without a steering service the composer says steering is unavailable and offers no send button.</summary>
    [Fact]
    public void WithoutASteeringServiceTheComposerSaysSo()
    {
        SetupScreen();
        AddTransport((_, _, token) => ScriptedTerminalTransport.StreamAsync([], hold: true, cancellationToken: token));

        var cut = RenderTerminal();

        cut.Find("[role='group'][aria-label='" + TerminalCopy.SteerName + "']").TextContent.ShouldContain(TerminalCopy.SteerUnavailable);
        cut.FindAll("textarea").Count.ShouldBe(0);
        cut.FindAll(".autonogrammer-terminal-steer button").Count.ShouldBe(0);
    }

    /// <summary>Disposing the view releases the screen, and every named control carries an accessible name with no inline script or style.</summary>
    [Fact]
    public async Task DisposingReleasesTheScreenAndTheMarkupIsSafe()
    {
        var module = SetupScreen();
        AddTransport((_, _, token) => ScriptedTerminalTransport.StreamAsync([(1, "x")], hold: true, cancellationToken: token));
        Services.AddSingleton<ICardSteering>(new WorkspaceCardSteering());
        var cut = RenderTerminal();
        WaitForState(cut, "live");

        foreach (var element in cut.FindAll("button, textarea, [role='group']"))
        {
            (element.HasAttribute("aria-label") || element.TextContent.Trim().Length > 0).ShouldBeTrue("an element has no accessible name: " + element.OuterHtml);
        }

        cut.Markup.ShouldNotContain("<script", Case.Insensitive);
        cut.FindAll("[style]").Count.ShouldBe(0);

        await DisposeComponentsAsync().ConfigureAwait(true);

        module.VerifyInvoke("dispose").Arguments.ShouldBe([ScreenToken]);
    }

    private const int ScreenToken = 7;

    private static List<string> WrittenText(BunitJSModuleInterop module) =>
        module.Invocations.Identifiers.Contains("write", StringComparer.Ordinal) ? module.Invocations["write"].Select(static call => (string)call.Arguments[1]!).ToList() : [];

    private BunitJSModuleInterop SetupScreen()
    {
        var module = JSInterop.SetupModule(TerminalModule);
        module.Setup<int>("create", _ => true).SetResult(ScreenToken);
        return module;
    }

    private ScriptedTerminalTransport AddTransport(Func<int, long, CancellationToken, IAsyncEnumerable<RemoteTerminalChunk>> script)
    {
        var transport = new ScriptedTerminalTransport(script);
        Services.AddSingleton<IRemoteTerminalTransport>(transport);
        return transport;
    }

    private IRenderedComponent<TerminalView> RenderTerminal(TimeSpan? reconnectDelay = null) =>
        Render<TerminalView>(parameters => parameters
            .Add(p => p.SessionId, SessionId)
            .Add(p => p.ReconnectDelay, reconnectDelay ?? TimeSpan.Zero));

    private static void WaitForState(IRenderedComponent<TerminalView> cut, string state) =>
        cut.WaitForAssertion(() => cut.Find(".autonogrammer-terminal").GetAttribute("data-state").ShouldBe(state));
}
