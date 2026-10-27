using System.Text.Json;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Controls;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Streaming;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Controls;

/// <summary>Verifies the control shapes of contract 1.3.0 and the two stream kinds it adds, which older readers still deserialize as unknown events.</summary>
public sealed class ControlsContractJsonTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The steering body reads its lower-case mode names and refuses an unknown mode with a JSON error.</summary>
    [Fact]
    public void SteerRequest_ReadsItsModes()
    {
        var request = JsonSerializer.Deserialize("""{"text":"left","mode":"send_now"}""", SessionControlsJsonContext.Default.SteerRequest).ShouldNotBeNull();
        request.Mode.ShouldBe(SteerMode.SendNow);
        request.Text.ShouldBe("left", Case.Sensitive);
        JsonSerializer.Deserialize("""{"text":"x","mode":"queue"}""", SessionControlsJsonContext.Default.SteerRequest).ShouldNotBeNull().Mode.ShouldBe(SteerMode.Queue);
        JsonSerializer.Deserialize("""{"text":"x"}""", SessionControlsJsonContext.Default.SteerRequest).ShouldNotBeNull().Mode.ShouldBeNull();
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize("""{"text":"x","mode":"later"}""", SessionControlsJsonContext.Default.SteerRequest));
    }

    /// <summary>Each delivery has its wire name and the reason is omitted when absent.</summary>
    [Theory]
    [InlineData(SteerDelivery.Delivered, "delivered")]
    [InlineData(SteerDelivery.Queued, "queued")]
    [InlineData(SteerDelivery.Held, "held")]
    [InlineData(SteerDelivery.Failed, "failed")]
    public void SteerResponse_UsesTheDeliveryWireName(SteerDelivery delivery, string wire)
    {
        var json = JsonSerializer.Serialize(new SteerResponse { Delivery = delivery }, SessionControlsJsonContext.Default.SteerResponse);

        json.ShouldBe($$"""{"delivery":"{{wire}}"}""", Case.Sensitive);
    }

    /// <summary>The option, command, change and approval answers round trip.</summary>
    [Fact]
    public void OptionsCommandsChangesAndApprovals_RoundTrip()
    {
        var options = new SessionConfigOptionsResponse
        {
            SessionId = "s1",
            Available = true,
            Live = false,
            Reason = "last known",
            Options = [new ConfigOption { Id = "mode", Name = "Mode", Category = "mode", CurrentValue = "agent", Values = [new ConfigOptionValue { Value = "agent", Name = "Agent" }] }],
        };
        var commands = new SessionCommandsResponse { SessionId = "s1", Available = true, Live = true, Commands = [new SessionCommand { Name = "plan", InputHint = "[x]" }] };
        var changes = new SessionChangesResponse
        {
            SessionId = "s1",
            WorkspaceAvailable = true,
            Changes = [new SessionChange { Path = "a.txt", Kind = ChangeKind.Added, Status = ChangeReviewStatus.Accepted, NewText = "x" }],
        };
        var approvals = new PendingToolApprovalsResponse
        {
            SessionId = "s1",
            Approvals = [new PendingToolApproval { RequestId = "r:1", ToolName = "Bash", RequestedAt = Stamp, GrantableTools = [] }],
        };

        JsonSerializer.Deserialize(JsonSerializer.Serialize(options, SessionControlsJsonContext.Default.SessionConfigOptionsResponse), SessionControlsJsonContext.Default.SessionConfigOptionsResponse)
            .ShouldNotBeNull().Options.ShouldHaveSingleItem().Values.ShouldHaveSingleItem().Value.ShouldBe("agent", Case.Sensitive);
        JsonSerializer.Deserialize(JsonSerializer.Serialize(commands, SessionControlsJsonContext.Default.SessionCommandsResponse), SessionControlsJsonContext.Default.SessionCommandsResponse)
            .ShouldNotBeNull().Commands.ShouldHaveSingleItem().InputHint.ShouldBe("[x]", Case.Sensitive);
        var changesJson = JsonSerializer.Serialize(changes, SessionControlsJsonContext.Default.SessionChangesResponse);
        changesJson.ShouldContain("\"kind\":\"added\"", Case.Sensitive);
        changesJson.ShouldContain("\"status\":\"accepted\"", Case.Sensitive);
        changesJson.ShouldNotContain("oldText", Case.Sensitive);
        JsonSerializer.Deserialize(JsonSerializer.Serialize(approvals, SessionControlsJsonContext.Default.PendingToolApprovalsResponse), SessionControlsJsonContext.Default.PendingToolApprovalsResponse)
            .ShouldNotBeNull().Approvals.ShouldHaveSingleItem().RequestId.ShouldBe("r:1", Case.Sensitive);
    }

    /// <summary>A stage event travels in a stream event as the stage kind, deserializes as an unknown payload with the kind preserved, and is read back from it.</summary>
    [Fact]
    public void StagePayload_IsAnUnknownPayloadToTheCoreContext_AndReadsBack()
    {
        var stage = new StageEvent { SessionId = "s1", Source = StageSource.Ultrathink, StageId = "plan", Title = "Plan", Status = StageStatus.Degraded, Timestamp = Stamp, Detail = "slow", GraphId = "g" };
        var evt = new StreamEvent { Seq = 4, SessionId = "s1", CorrelationId = "c", Timestamp = Stamp, Payload = StreamEventPayloads.CreateStage(stage) };

        var json = JsonSerializer.Serialize(evt, OmpOsJsonContext.Default.StreamEvent);
        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.StreamEvent).ShouldNotBeNull();

        json.ShouldContain("\"kind\":\"stage\"", Case.Sensitive);
        var unknown = back.Payload.ShouldBeOfType<UnknownPayload>();
        unknown.OriginalKind.ShouldBe(StreamEventKinds.Stage, Case.Sensitive);
        JsonSerializer.Serialize(back, OmpOsJsonContext.Default.StreamEvent).ShouldBe(json, Case.Sensitive);
        StreamEventPayloads.TryReadStage(back.Payload, out var read).ShouldBeTrue();
        read.ShouldBe(stage);
        StreamEventPayloads.TryReadConfigOptionUpdate(back.Payload, out _).ShouldBeFalse();
    }

    /// <summary>A configuration update travels the same way, and a kind this version does not know is still an unknown payload that the readers refuse.</summary>
    [Fact]
    public void ConfigUpdatePayload_ReadsBack_AndUnknownKindsStayUnknown()
    {
        var update = new ConfigOptionUpdateEvent { OptionId = "model", CurrentValue = "provider/model" };
        var payload = StreamEventPayloads.CreateConfigOptionUpdate(update);
        payload.OriginalKind.ShouldBe(StreamEventKinds.ConfigOptionUpdate, Case.Sensitive);
        StreamEventPayloads.TryReadConfigOptionUpdate(payload, out var read).ShouldBeTrue();
        read.ShouldBe(update);

        var future = JsonSerializer.Deserialize("""{"kind":"not_a_kind_yet","x":1}""", OmpOsJsonContext.Default.StreamEventPayload).ShouldNotBeNull();
        future.ShouldBeOfType<UnknownPayload>().OriginalKind.ShouldBe("not_a_kind_yet", Case.Sensitive);
        StreamEventPayloads.TryReadStage(future, out var none).ShouldBeFalse();
        none.ShouldBeNull();
        using var document = JsonDocument.Parse("""{"kind":"stage","sessionId":5}""");
        var malformed = new UnknownPayload { OriginalKind = StreamEventKinds.Stage, Raw = document.RootElement.Clone() };
        StreamEventPayloads.TryReadStage(malformed, out _).ShouldBeFalse();
    }
}
