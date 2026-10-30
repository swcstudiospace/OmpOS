using System.Text.Json;
using Autonogrammer.Contracts.Hooks;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>A run record leaves the process only as a redacted contract event, and that event round-trips through the stream payload while an older client reads it as unknown.</summary>
public sealed class HookRunRecordProjectorTests
{
    private static HookRunRecord Record(string? stdout = null, string? userMessage = null) => new(
        new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero),
        "s1",
        "g1",
        "beforeShellExecution",
        "cursor-project:abc:guard",
        "cursor-project:abc",
        HookScope.ClaudeProject,
        "Shell",
        HookRunStatus.TimedOut,
        "none",
        null,
        TimeSpan.FromMilliseconds(30250),
        userMessage,
        stdout,
        null,
        true,
        "00ff",
        "timed out");

    /// <summary>Every field is projected with the contract's names for the scope and the status.</summary>
    [Fact]
    public void AllFieldsAreProjected()
    {
        var run = HookRunRecordProjector.Project(Record());

        run.At.ShouldBe(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));
        run.SessionId.ShouldBe("s1");
        run.GenerationId.ShouldBe("g1");
        run.EventName.ShouldBe("beforeShellExecution");
        run.HookId.ShouldBe("cursor-project:abc:guard");
        run.Scope.ShouldBe("claudeProject");
        run.Status.ShouldBe("timedOut");
        run.Decision.ShouldBe("none");
        run.DurationMs.ShouldBe(30250);
        run.MatchedValue.ShouldBe("Shell");
        run.OutputTruncated.ShouldBeTrue();
        run.PayloadSha256.ShouldBe("00ff");
        run.Note.ShouldBe("timed out");
    }

    /// <summary>Credentials in the output and the message are redacted before the record leaves the process.</summary>
    [Fact]
    public void TheOutputAndTheMessageAreRedacted()
    {
        var run = HookRunRecordProjector.Project(Record("token=abc123secretvalue and Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dBjftJeZ4CVPmB92K27uhbUJU1p1r_wW1gFWFOEjXk", "uses key sk-abcdefghijklmnopqrstuvwx"));

        run.StandardOutputExcerpt.ShouldNotBeNull().ShouldNotContain("abc123secretvalue", Case.Sensitive);
        run.StandardOutputExcerpt.ShouldNotContain("eyJhbGciOiJIUzI1NiJ9", Case.Sensitive);
        run.StandardOutputExcerpt.ShouldContain(HookLogRedaction.Marker, Case.Sensitive);
        run.UserMessage.ShouldNotBeNull().ShouldNotContain("sk-abcdefghijklmnopqrstuvwx", Case.Sensitive);
    }

    /// <summary>The payload is an unknown payload of kind hook, reads back to the same event, and a payload of another kind does not read as a hook.</summary>
    [Fact]
    public void ThePayloadRoundTripsAndOtherKindsAreNotHooks()
    {
        var payload = HookRunRecordProjector.ToPayload(Record("ok"));

        payload.OriginalKind.ShouldBe("hook");
        payload.Raw.GetProperty("kind").GetString().ShouldBe("hook");
        HookStreamPayload.TryRead(payload, out var read).ShouldBeTrue();
        read.ShouldBe(HookRunRecordProjector.Project(Record("ok")));
        HookStreamPayload.TryRead(new DiagnosticPayload { Message = "x" }, out var none).ShouldBeFalse();
        none.ShouldBeNull();
        using var other = JsonDocument.Parse("{\"kind\":\"hook\",\"at\":\"not a date\"}");
        HookStreamPayload.TryRead(new UnknownPayload { OriginalKind = "hook", Raw = other.RootElement.Clone() }, out _).ShouldBeFalse();
    }

    /// <summary>A stream event that carries the payload serializes with the kind first and deserializes to an unknown payload of kind hook, as an older client sees it.</summary>
    [Fact]
    public void AnOlderClientReadsTheEventAsAnUnknownPayload()
    {
        var payload = HookRunRecordProjector.ToPayload(Record());
        var json = JsonSerializer.Serialize<StreamEventPayload>(payload, AutonogrammerJsonContext.Default.StreamEventPayload);

        var back = JsonSerializer.Deserialize(json, AutonogrammerJsonContext.Default.StreamEventPayload);

        var unknown = back.ShouldBeOfType<UnknownPayload>();
        unknown.OriginalKind.ShouldBe("hook");
        HookStreamPayload.TryRead(unknown, out var read).ShouldBeTrue();
        read.ShouldNotBeNull().HookId.ShouldBe("cursor-project:abc:guard");
    }
}
