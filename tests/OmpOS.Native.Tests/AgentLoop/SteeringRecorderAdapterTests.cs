using System.Security.Cryptography;
using System.Text;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Substrate;
using OmpOS.Native.Agents;
using OmpOS.Native.Tests.RunControls;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Runs;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentLoop;

/// <summary>
/// The adapter between the steering history and the Substrate ledger recorder: the record carries the mode, the length and the hash
/// prefix of the message and never the message, and a record that fails changes nothing about the message that was queued.
/// </summary>
public sealed class SteeringRecorderAdapterTests
{
    private const string Message = "Please switch to the smaller model. api_key=sk-test-1234";

    private static StageObservationScope Scope() => new()
    {
        SessionId = "s-test",
        WorkspaceRoot = "/work",
        SessionStateDirectory = "/state/sessions/s-test",
    };

    private static string HashPrefix(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];

    private static SteeringInputModel Steering(RecordingRunInput input, SteeringRecorderAdapter adapter)
    {
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();
        return new SteeringInputModel(scheduler, TimeProvider.System, adapter.RecordAsync);
    }

    /// <summary>A steer and a follow-up are recorded with their own mode, the length of the text and the 12-character SHA-256 prefix; the text is not part of the record.</summary>
    [Theory]
    [InlineData(SteeringMode.Steer, "steer")]
    [InlineData(SteeringMode.FollowUp, "follow-up")]
    public async Task AnEntryIsRecordedWithItsModeLengthAndHashPrefixAndNeverItsText(SteeringMode mode, string expectedMode)
    {
        var ct = TestContext.Current.CancellationToken;
        var recorder = new CapturingRecorder(new SteeringRecordResult(SteeringRecordOutcome.Recorded, null));
        var adapter = new SteeringRecorderAdapter(recorder, new SessionScopeProvider(Scope()));
        using var steering = Steering(new RecordingRunInput(), adapter);

        (await steering.SubmitAsync(Message, mode, ct).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);

        var call = recorder.Calls.ShouldHaveSingleItem();
        call.Record.Mode.ShouldBe(expectedMode);
        call.Record.Length.ShouldBe(Message.Length);
        call.Record.TextSha256Prefix.ShouldBe(HashPrefix(Message));
        call.Record.TextSha256Prefix.Length.ShouldBe(12);
        call.Record.ToString().ShouldNotContain("api_key");
        call.Record.ToString().ShouldNotContain("smaller model");
        steering.History.Single().RecordStatus.ShouldBe(SteeringRecordStatus.Recorded);
    }

    /// <summary>The recorder is given the scope the session has when the message is recorded, including the identifiers that appeared after the session opened.</summary>
    [Fact]
    public async Task TheRecorderSeesTheScopeTheSessionHasNow()
    {
        var ct = TestContext.Current.CancellationToken;
        var scope = new SessionScopeProvider(Scope());
        var recorder = new CapturingRecorder(new SteeringRecordResult(SteeringRecordOutcome.Recorded, null));
        using var steering = Steering(new RecordingRunInput(), new SteeringRecorderAdapter(recorder, scope));
        scope.Update("native-9", "ut-graph-1");

        await steering.SubmitAsync(Message, SteeringMode.Steer, ct).ConfigureAwait(true);

        var seen = recorder.Calls.ShouldHaveSingleItem().Scope;
        seen.AcpSessionId.ShouldBe("native-9");
        seen.GraphId.ShouldBe("ut-graph-1");
        seen.SessionId.ShouldBe("s-test");
    }

    /// <summary>A record that is skipped or failed marks only its own history entry: the message was already delivered to the run.</summary>
    [Theory]
    [InlineData(SteeringRecordOutcome.Skipped)]
    [InlineData(SteeringRecordOutcome.Failed)]
    public async Task AnUnrecordedEntryNeverLosesTheQueuedMessage(SteeringRecordOutcome outcome)
    {
        var ct = TestContext.Current.CancellationToken;
        var recorder = new CapturingRecorder(new SteeringRecordResult(outcome, "The Substrate is not available."));
        var input = new RecordingRunInput();
        using var steering = Steering(input, new SteeringRecorderAdapter(recorder, new SessionScopeProvider(Scope())));

        (await steering.SubmitAsync(Message, SteeringMode.Steer, ct).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);

        input.Steers.ShouldBe([Message]);
        steering.History.Single().RecordStatus.ShouldBe(SteeringRecordStatus.RecordFailed);
    }

    /// <summary>Through the real ledger recorder, the request that leaves the head holds the hash prefix and the length, the token only in the Authorization header, and nothing of the message.</summary>
    [Fact]
    public async Task TheLedgerRequestNeverCarriesTheMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new StubSubstrateHandler();
        using var client = new HttpClient(handler, disposeHandler: false);
        var gateway = new SubstrateGateway(client, SubstrateEndpoint.Parse("http://127.0.0.1:7410"), () => "bearer-secret", TimeProvider.System);
        var adapter = new SteeringRecorderAdapter(new SubstrateSteeringRecorder(gateway), new SessionScopeProvider(Scope()));
        using var steering = Steering(new RecordingRunInput(), adapter);

        await steering.SubmitAsync(Message, SteeringMode.Steer, ct).ConfigureAwait(true);

        var emit = handler.Requests.Single(static request => string.Equals(request.Path, "/events", StringComparison.Ordinal));
        emit.Body.ShouldContain(HashPrefix(Message));
        emit.Body.ShouldNotContain("api_key");
        emit.Body.ShouldNotContain("smaller model");
        emit.Body.ShouldNotContain("bearer-secret");
        emit.Authorization.ShouldBe("Bearer bearer-secret");
        steering.History.Single().RecordStatus.ShouldBe(SteeringRecordStatus.Recorded);
    }

    /// <summary>The recorder is not told about a null entry.</summary>
    [Fact]
    public void ANullEntryIsRefused()
    {
        Should.Throw<ArgumentNullException>(() => { _ = SteeringRecorderAdapter.ToRecord(null!); });
    }

    private sealed class CapturingRecorder(SteeringRecordResult result) : ISteeringRecorder
    {
        public List<(StageObservationScope Scope, SteeringRecord Record)> Calls { get; } = [];

        public Task<SteeringRecordResult> RecordAsync(StageObservationScope scope, SteeringRecord record, CancellationToken cancellationToken)
        {
            Calls.Add((scope, record));
            return Task.FromResult(result);
        }
    }
}
