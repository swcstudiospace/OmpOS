using System.Text.Json;
using Autonogrammer.Contracts.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>The <c>_autonogrammer/hook</c> notification of an Autonogrammer agent worker: its wire form, how a client reads it, and what a session does with it.</summary>
public sealed class AcpHookNotificationTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 28, 9, 30, 0, TimeSpan.Zero);

    private static AcpClientOptions Options => new() { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" };

    private static HookRunEvent Run(string status = "denied") => new()
    {
        At = At,
        SessionId = "cloud-1",
        GenerationId = "g1",
        EventName = "beforeShellExecution",
        HookId = "project:guard",
        Scope = "project",
        Status = status,
        Decision = "deny",
        ExitCode = 0,
        DurationMs = 12,
        MatchedValue = "rm -rf /",
        UserMessage = "Blocked by the project's guard hook.",
        StandardOutputExcerpt = "{\"permission\":\"deny\"}",
        OutputTruncated = false,
        PayloadSha256 = new string('a', 64),
    };

    private static JsonElement? ParamsOf(byte[] frame)
    {
        using var document = JsonDocument.Parse(frame);
        return document.RootElement.GetProperty("params").Clone();
    }

    /// <summary>A run survives the wire: the frame is one JSON-RPC notification with the extension method, and reading its params gives the same run back.</summary>
    [Fact]
    public void ARunRoundTripsThroughItsFrame()
    {
        var frame = AcpHookNotificationFrames.Serialize(new AcpHookNotification { Run = Run() });

        using var document = JsonDocument.Parse(frame);
        document.RootElement.GetProperty("jsonrpc").GetString().ShouldBe("2.0");
        document.RootElement.GetProperty("method").GetString().ShouldBe("_autonogrammer/hook");
        document.RootElement.TryGetProperty("id", out _).ShouldBeFalse();
        document.RootElement.GetProperty("params").GetProperty("version").GetInt32().ShouldBe(1);
        System.Text.Encoding.UTF8.GetString(frame).ShouldNotContain("\n", Case.Sensitive);
        AcpHookNotificationFrames.TryParse(ParamsOf(frame), out var notification).ShouldBeTrue();
        notification.ShouldNotBeNull();
        notification.State.ShouldBeNull();
        notification.Run.ShouldBe(Run());
    }

    /// <summary>A state frame carries the state, the count and the reason, and no run.</summary>
    [Fact]
    public void AStateRoundTripsThroughItsFrame()
    {
        var frame = AcpHookNotificationFrames.Serialize(new AcpHookNotification { State = AcpHookNotification.StateUntrusted, HookCount = 3, Reason = "No snapshot." });

        AcpHookNotificationFrames.TryParse(ParamsOf(frame), out var notification).ShouldBeTrue();
        notification.ShouldNotBeNull();
        notification.Run.ShouldBeNull();
        notification.State.ShouldBe("untrusted");
        notification.HookCount.ShouldBe(3);
        notification.Reason.ShouldBe("No snapshot.");
    }

    /// <summary>A notification must carry exactly one of a run and a state.</summary>
    [Fact]
    public void ANotificationWithBothOrNeitherIsRefused()
    {
        Should.Throw<ArgumentException>(() => AcpHookNotificationFrames.Serialize(new AcpHookNotification()));
        Should.Throw<ArgumentException>(() => AcpHookNotificationFrames.Serialize(new AcpHookNotification { Run = Run(), State = "active" }));
    }

    /// <summary>Params of another version, without a version, or without a run or a state are not read: an older client ignores what it does not know.</summary>
    [Theory]
    [InlineData("{\"version\":2,\"state\":\"active\"}")]
    [InlineData("{\"state\":\"active\"}")]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"run\":\"text\"}")]
    [InlineData("{\"version\":1,\"run\":{\"sessionId\":\"x\"}}")]
    [InlineData("{\"version\":1,\"state\":\"\"}")]
    [InlineData("[1]")]
    public void UnknownOrBrokenParamsAreNotRead(string json)
    {
        using var document = JsonDocument.Parse(json);

        AcpHookNotificationFrames.TryParse(document.RootElement, out var notification).ShouldBeFalse();
        notification.ShouldBeNull();
        AcpHookNotificationFrames.TryParse(null, out _).ShouldBeFalse();
    }

    /// <summary>A session raises the event for a worker's notification, in wire order, with the run as a <c>hook</c> stream payload.</summary>
    [Fact]
    public async Task ASessionRaisesTheEventWithTheRunAndItsStreamPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using (session.ConfigureAwait(true))
        {
            await session.CreateAsync(ct).ConfigureAwait(true);
            var received = new List<AcpHookNotificationEventArgs>();
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            session.HookNotificationReceived += (_, args) =>
            {
                lock (received)
                {
                    received.Add(args);
                    if (received.Count == 2)
                    {
                        done.TrySetResult();
                    }
                }
            };

            transport.Deliver(AcpHookNotificationFrames.Serialize(new AcpHookNotification { State = AcpHookNotification.StateActive, HookCount = 1 }));
            transport.Deliver(AcpHookNotificationFrames.Serialize(new AcpHookNotification { Run = Run() }));
            await done.Task.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(true);

            lock (received)
            {
                received[0].Notification.State.ShouldBe("active");
                received[0].StreamPayload.ShouldBeNull();
                received[1].Notification.Run.ShouldBe(Run());
                var payload = received[1].StreamPayload.ShouldNotBeNull();
                payload.OriginalKind.ShouldBe("hook");
                HookStreamPayload.TryRead(payload, out var read).ShouldBeTrue();
                read.ShouldBe(Run());
            }
        }
    }

    /// <summary>A handler that throws does not break the session: the next frames are still read and the session still answers requests.</summary>
    [Fact]
    public async Task AThrowingHandlerDoesNotBreakTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using (session.ConfigureAwait(true))
        {
            await session.CreateAsync(ct).ConfigureAwait(true);
            var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            session.HookNotificationReceived += (_, _) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    throw new InvalidOperationException("a subscriber that fails");
                }

                second.TrySetResult();
            };

            transport.Deliver(AcpHookNotificationFrames.Serialize(new AcpHookNotification { Run = Run("failed") }));
            transport.Deliver(AcpHookNotificationFrames.Serialize(new AcpHookNotification { Run = Run("succeeded") }));
            await second.Task.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(true);

            calls.ShouldBe(2);
        }
    }
}
