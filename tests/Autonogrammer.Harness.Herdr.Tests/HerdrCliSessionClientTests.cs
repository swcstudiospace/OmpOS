using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Herdr.Tests;

public sealed class HerdrCliSessionClientTests
{
    private const string ExecutablePath = "herdr-under-test";
    private const string SessionReference = "/sessions/alpha.jsonl";
    private const string UnsupportedReason = "herdr 0.9.5 has no pause, resume, attach or detach command";
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);
    private static readonly CancellationToken NoCancellation = CancellationToken.None;

    [Fact]
    public async Task ListAsync_ReturnsOnlyNamedAgentsSortedWithTheirNativeReference()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(Ok("""
            {"agents":[
              {"name":"beta","agent":"omp","agent_status":"idle","pane_id":"wM:p9","workspace_id":"wM","tab_id":"wM:t9","terminal_id":"term_1","focused":false,"revision":3,"state_change_seq":11,"agent_session":{"agent":"omp","kind":"path","source":"herdr:omp","value":"/sessions/beta.jsonl"}},
              {"agent":"omp","agent_status":"working","pane_id":"wM:p2","workspace_id":"wM","tab_id":"wM:t2","terminal_id":"term_2","focused":false,"revision":4,"state_change_seq":12},
              {"name":"alpha","agent":"omp","agent_status":"working","pane_id":"wM:p1","workspace_id":"wM","tab_id":"wM:t1","terminal_id":"term_3","focused":true,"revision":5,"state_change_seq":2}
            ]}
            """));

        IReadOnlyList<HerdrSessionSnapshot> sessions = await client.ListAsync(NoCancellation).ConfigureAwait(true);

        Lines(runner).ShouldBe(["agent|list"]);
        sessions.Select(session => session.Id.Value).ShouldBe(["alpha", "beta"]);
        sessions[0].State.ShouldBe(HerdrSessionState.Running);
        sessions[0].IsAttached.ShouldBeFalse();
        sessions[0].NativeReference.ShouldBeNull();
        sessions[0].LastSequence.ShouldBe(2);
        sessions[1].NativeReference?.Value.ShouldBe("/sessions/beta.jsonl");
        sessions[1].LastSequence.ShouldBe(11);
    }

    [Fact]
    public async Task StatusAsync_ReadsTheAgentAndItsNativeReference()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(AgentResult(AgentJson("alpha", "wM3:p1", 7, SessionReference)));

        HerdrSessionSnapshot snapshot = await client.StatusAsync(new HerdrSessionId("alpha"), NoCancellation).ConfigureAwait(true);

        runner.ExecutablePath.ShouldBe(ExecutablePath);
        Lines(runner).ShouldBe(["agent|get|alpha"]);
        snapshot.Id.Value.ShouldBe("alpha");
        snapshot.State.ShouldBe(HerdrSessionState.Running);
        snapshot.NativeReference?.Value.ShouldBe(SessionReference);
        snapshot.LastSequence.ShouldBe(7);
    }

    [Fact]
    public async Task StatusAsync_MapsAgentNotFoundToNotFound()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(Failure("agent_not_found", "agent target alpha not found"));

        await Should.ThrowAsync<HerdrSessionNotFoundException>(
            () => client.StatusAsync(new HerdrSessionId("alpha"), NoCancellation)).ConfigureAwait(true);

        Lines(runner).ShouldBe(["agent|get|alpha"]);
    }

    [Fact]
    public async Task StatusAsync_NonJsonOutputFailsWithoutBeingReportedAsNotFound()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(new HerdrProcessResult(1, "not json", "boom"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => client.StatusAsync(new HerdrSessionId("alpha"), NoCancellation)).ConfigureAwait(true);
    }

    [Fact]
    public async Task StatusAsync_NonJsonOutputNamesBoundedExcerptsOfBothStreams()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(new HerdrProcessResult(1, new string('~', 300), new string('^', 300)));

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            () => client.StatusAsync(new HerdrSessionId("alpha"), NoCancellation)).ConfigureAwait(true);

        failure.Message.ShouldContain(new string('~', 200));
        failure.Message.ShouldNotContain(new string('~', 201));
        failure.Message.ShouldContain(new string('^', 200));
        failure.Message.ShouldNotContain(new string('^', 201));
        failure.Message.ShouldNotContain("alpha");
        Lines(runner).ShouldBe(["agent|get|alpha"]);
    }

    [Fact]
    public async Task StatusAsync_ErrorEnvelopeOnStdoutWithEmptyStderrIsMapped()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(new HerdrProcessResult(1, ErrorEnvelope("agent_not_found", "agent target alpha not found"), string.Empty));

        await Should.ThrowAsync<HerdrSessionNotFoundException>(
            () => client.StatusAsync(new HerdrSessionId("alpha"), NoCancellation)).ConfigureAwait(true);

        Lines(runner).ShouldBe(["agent|get|alpha"]);
    }

    [Fact]
    public async Task CreateAsync_NonJsonStdoutWithNotFoundEnvelopeOnStderrReadsAsAbsentAgent()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(new HerdrProcessResult(1, "not json", ErrorEnvelope("agent_not_found", "agent target alpha not found")));
        runner.Enqueue(Ok("""{"workspace":{"workspace_id":"wM3"}}"""));
        runner.Enqueue(Ok("""{"panes":[{"pane_id":"wM3:p1"}]}"""));
        runner.Enqueue(Ok("""{"agent":{"name":"alpha"}}"""));
        runner.Enqueue(AgentResult(AgentJson("alpha", "wM3:p1", 1, null)));

        HerdrSessionSnapshot snapshot = await client.CreateAsync(
            new HerdrCreateRequest(new HerdrSessionId("alpha"), "omp", null),
            NoCancellation).ConfigureAwait(true);

        snapshot.State.ShouldBe(HerdrSessionState.Running);
        Lines(runner).ShouldBe(
        [
            "agent|get|alpha",
            "workspace|create|--label|alpha|--no-focus",
            "pane|list|--workspace|wM3",
            "agent|start|alpha|--kind|omp|--pane|wM3:p1",
            "agent|get|alpha",
        ]);
    }

    [Fact]
    public async Task CreateAsync_StartsANewAgentInANewWorkspace()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(Failure("agent_not_found", "agent target alpha not found"));
        runner.Enqueue(Ok("""{"workspace":{"workspace_id":"wM3"}}"""));
        runner.Enqueue(Ok("""{"panes":[{"pane_id":"wM3:p1"}]}"""));
        runner.Enqueue(Ok("""{"agent":{"name":"alpha"}}"""));
        runner.Enqueue(AgentResult(AgentJson("alpha", "wM3:p1", 1, null)));

        HerdrSessionSnapshot snapshot = await client.CreateAsync(
            new HerdrCreateRequest(new HerdrSessionId("alpha"), "omp", null),
            NoCancellation).ConfigureAwait(true);

        Lines(runner).ShouldBe(
        [
            "agent|get|alpha",
            "workspace|create|--label|alpha|--no-focus",
            "pane|list|--workspace|wM3",
            "agent|start|alpha|--kind|omp|--pane|wM3:p1",
            "agent|get|alpha",
        ]);
        snapshot.State.ShouldBe(HerdrSessionState.Running);
        snapshot.NativeReference.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_ResumesFromTheStoredNativeReference()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(Failure("agent_not_found", "agent target alpha not found"));
        runner.Enqueue(Ok("""{"workspace":{"workspace_id":"wM3"}}"""));
        runner.Enqueue(Ok("""{"panes":[{"pane_id":"wM3:p1"}]}"""));
        runner.Enqueue(Ok("""{"agent":{"name":"alpha"}}"""));
        runner.Enqueue(AgentResult(AgentJson("alpha", "wM3:p1", 3, SessionReference)));

        HerdrSessionSnapshot snapshot = await client.CreateAsync(
            new HerdrCreateRequest(new HerdrSessionId("alpha"), "omp", new NativeSessionReference(SessionReference)),
            NoCancellation).ConfigureAwait(true);

        Lines(runner).ShouldBe(
        [
            "agent|get|alpha",
            "workspace|create|--label|alpha|--no-focus",
            "pane|list|--workspace|wM3",
            "agent|start|alpha|--kind|omp|--pane|wM3:p1|--|--resume=/sessions/alpha.jsonl",
            "agent|get|alpha",
        ]);
        snapshot.NativeReference?.Value.ShouldBe(SessionReference);
    }

    [Fact]
    public async Task CreateAsync_RefusesAnAgentThatAlreadyExists()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(AgentResult(AgentJson("alpha", "wM3:p1", 1, null)));

        await Should.ThrowAsync<HerdrSessionConflictException>(
            () => client.CreateAsync(
                new HerdrCreateRequest(new HerdrSessionId("alpha"), "omp", null),
                NoCancellation)).ConfigureAwait(true);

        Lines(runner).ShouldBe(["agent|get|alpha"]);
    }

    [Fact]
    public async Task CreateAsync_ClosesTheWorkspaceWhenTheAgentDoesNotStart()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(Failure("agent_not_found", "agent target alpha not found"));
        runner.Enqueue(Ok("""{"workspace":{"workspace_id":"wM3"}}"""));
        runner.Enqueue(Ok("""{"panes":[{"pane_id":"wM3:p1"}]}"""));
        runner.Enqueue(Failure("agent_start_failed", "the agent never became ready"));
        runner.Enqueue(Ok("""{}"""));

        await Should.ThrowAsync<InvalidOperationException>(
            () => client.CreateAsync(
                new HerdrCreateRequest(new HerdrSessionId("alpha"), "omp", null),
                NoCancellation)).ConfigureAwait(true);

        Lines(runner).ShouldBe(
        [
            "agent|get|alpha",
            "workspace|create|--label|alpha|--no-focus",
            "pane|list|--workspace|wM3",
            "agent|start|alpha|--kind|omp|--pane|wM3:p1",
            "workspace|close|wM3",
        ]);
    }

    [Fact]
    public async Task CreateAsync_RetriesTheAgentStartWhilePaneIsBusyThenSucceeds()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(Failure("agent_not_found", "agent target alpha not found"));
        runner.Enqueue(Ok("""{"workspace":{"workspace_id":"wM3"}}"""));
        runner.Enqueue(Ok("""{"panes":[{"pane_id":"wM3:p1"}]}"""));
        runner.Enqueue(Failure("agent_pane_busy", "agent target pane wM3:p1 is not an available shell"));
        runner.Enqueue(Ok("""{"agent":{"name":"alpha"}}"""));
        runner.Enqueue(AgentResult(AgentJson("alpha", "wM3:p1", 1, null)));

        HerdrSessionSnapshot snapshot = await client.CreateAsync(
            new HerdrCreateRequest(new HerdrSessionId("alpha"), "omp", null),
            NoCancellation).ConfigureAwait(true);

        Lines(runner).ShouldBe(
        [
            "agent|get|alpha",
            "workspace|create|--label|alpha|--no-focus",
            "pane|list|--workspace|wM3",
            "agent|start|alpha|--kind|omp|--pane|wM3:p1",
            "agent|start|alpha|--kind|omp|--pane|wM3:p1",
            "agent|get|alpha",
        ]);
        snapshot.State.ShouldBe(HerdrSessionState.Running);
    }

    [Fact]
    public async Task CreateAsync_FailsAtOnceOnAnAgentStartErrorThatIsNotBusy()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(Failure("agent_not_found", "agent target alpha not found"));
        runner.Enqueue(Ok("""{"workspace":{"workspace_id":"wM3"}}"""));
        runner.Enqueue(Ok("""{"panes":[{"pane_id":"wM3:p1"}]}"""));
        runner.Enqueue(Failure("agent_start_failed", "the agent never became ready"));
        runner.Enqueue(Ok("""{}"""));

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            () => client.CreateAsync(
                new HerdrCreateRequest(new HerdrSessionId("alpha"), "omp", null),
                NoCancellation)).ConfigureAwait(true);

        failure.Message.ShouldContain("agent_start_failed");
        Lines(runner).ShouldBe(
        [
            "agent|get|alpha",
            "workspace|create|--label|alpha|--no-focus",
            "pane|list|--workspace|wM3",
            "agent|start|alpha|--kind|omp|--pane|wM3:p1",
            "workspace|close|wM3",
        ]);
    }

    [Fact]
    public async Task TerminateAsync_ClosesThePaneAndReportsTerminated()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(AgentResult(AgentJson("alpha", "wM3:p1", 9, SessionReference)));
        runner.Enqueue(Ok("""{}"""));

        HerdrSessionSnapshot snapshot = await client.TerminateAsync(new HerdrSessionId("alpha"), NoCancellation).ConfigureAwait(true);

        Lines(runner).ShouldBe(["agent|get|alpha", "pane|close|wM3:p1"]);
        snapshot.State.ShouldBe(HerdrSessionState.Terminated);
        snapshot.IsAttached.ShouldBeFalse();
        snapshot.NativeReference?.Value.ShouldBe(SessionReference);
    }

    [Fact]
    public async Task TerminateAsync_MapsAgentNotFoundToNotFound()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(Failure("agent_not_found", "agent target alpha not found"));

        await Should.ThrowAsync<HerdrSessionNotFoundException>(
            () => client.TerminateAsync(new HerdrSessionId("alpha"), NoCancellation)).ConfigureAwait(true);

        Lines(runner).ShouldBe(["agent|get|alpha"]);
    }

    [Fact]
    public async Task TerminateAsync_UnmappedHerdrErrorIsNotReportedAsNotFound()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(AgentResult(AgentJson("alpha", "wM3:p1", 9, null)));
        runner.Enqueue(Failure("pane_close_refused", "the pane cannot be closed"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => client.TerminateAsync(new HerdrSessionId("alpha"), NoCancellation)).ConfigureAwait(true);

        Lines(runner).ShouldBe(["agent|get|alpha", "pane|close|wM3:p1"]);
    }

    [Fact]
    public void Capabilities_ReportsEveryOptionalOperationAsAbsent()
    {
        var (client, _) = NewClient();

        client.Capabilities.ShouldBe(new HerdrSessionCapabilities(Pause: false, Resume: false, Attach: false, Detach: false));
    }

    [Fact]
    public async Task AttachAsync_IsNotSupportedAndStartsNoProcess()
    {
        var (client, runner) = NewClient();

        var failure = await Should.ThrowAsync<HerdrOperationUnsupportedException>(
            () => client.AttachAsync(new HerdrSessionId("alpha"), 0, NoCancellation)).ConfigureAwait(true);

        failure.Operation.ShouldBe("Attach");
        failure.Reason.ShouldBe(UnsupportedReason);

        runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetachAsync_IsNotSupportedAndStartsNoProcess()
    {
        var (client, runner) = NewClient();

        var failure = await Should.ThrowAsync<HerdrOperationUnsupportedException>(
            () => client.DetachAsync(new HerdrSessionId("alpha"), NoCancellation)).ConfigureAwait(true);

        failure.Operation.ShouldBe("Detach");
        failure.Reason.ShouldBe(UnsupportedReason);

        runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task PauseAsync_IsNotSupportedAndStartsNoProcess()
    {
        var (client, runner) = NewClient();

        var failure = await Should.ThrowAsync<HerdrOperationUnsupportedException>(
            () => client.PauseAsync(new HerdrSessionId("alpha"), NoCancellation)).ConfigureAwait(true);

        failure.Operation.ShouldBe("Pause");
        failure.Reason.ShouldBe(UnsupportedReason);

        runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResumeAsync_IsNotSupportedAndStartsNoProcess()
    {
        var (client, runner) = NewClient();

        var failure = await Should.ThrowAsync<HerdrOperationUnsupportedException>(
            () => client.ResumeAsync(new HerdrSessionId("alpha"), NoCancellation)).ConfigureAwait(true);

        failure.Operation.ShouldBe("Resume");
        failure.Reason.ShouldBe(UnsupportedReason);

        runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task HeartbeatsAsync_BeatsOnceForEachLiveSessionPerListing()
    {
        var (client, runner) = NewClient();
        runner.Enqueue(Ok("""{"agents":[{"name":"alpha","agent":"omp","agent_status":"idle","pane_id":"wM3:p1","workspace_id":"wM3","tab_id":"wM3:t1","terminal_id":"term_9","focused":false,"revision":1,"state_change_seq":4}]}"""));

        var beats = new List<HerdrHeartbeat>();
        await foreach (HerdrHeartbeat beat in client.HeartbeatsAsync(NoCancellation))
        {
            beats.Add(beat);
            break;
        }

        Lines(runner).ShouldBe(["agent|list"]);
        beats.Count.ShouldBe(1);
        beats[0].Id.Value.ShouldBe("alpha");
        beats[0].State.ShouldBe(HerdrSessionState.Running);
        beats[0].LastSequence.ShouldBe(4);
    }

    private static (HerdrCliSessionClient Client, ScriptedHerdrProcessRunner Runner) NewClient()
    {
        var runner = new ScriptedHerdrProcessRunner();
        return (new HerdrCliSessionClient(runner, ExecutablePath, HeartbeatInterval), runner);
    }

    private static string[] Lines(ScriptedHerdrProcessRunner runner) =>
        runner.Calls.Select(call => string.Join('|', call)).ToArray();

    private static HerdrProcessResult Ok(string resultJson) =>
        new(0, $$"""{"id":"cli:test","result":{{resultJson}}}""", string.Empty);

    private static HerdrProcessResult AgentResult(string agentJson) => Ok($$"""{"agent":{{agentJson}}}""");

    private static HerdrProcessResult Failure(string code, string message) =>
        new(1, string.Empty, ErrorEnvelope(code, message));

    private static string ErrorEnvelope(string code, string message) =>
        "{\"id\":\"cli:test\",\"error\":{\"code\":\"" + code + "\",\"message\":\"" + message + "\"}}";

    private static string AgentJson(string name, string paneId, long sequence, string? reference)
    {
        string session = reference is null
            ? "null"
            : $$"""{"agent":"omp","kind":"path","source":"herdr:omp","value":"{{reference}}"}""";
        return $$"""{"name":"{{name}}","agent":"omp","agent_status":"idle","pane_id":"{{paneId}}","workspace_id":"wM3","tab_id":"wM3:t1","terminal_id":"term_9","focused":false,"revision":1,"state_change_seq":{{sequence}},"agent_session":{{session}}}""";
    }

    private sealed class ScriptedHerdrProcessRunner : IHerdrProcessRunner
    {
        private readonly Queue<HerdrProcessResult> _responses = new();

        public List<string[]> Calls { get; } = [];

        public string? ExecutablePath { get; private set; }

        public void Enqueue(HerdrProcessResult response) => _responses.Enqueue(response);

        public Task<HerdrProcessResult> RunAsync(string executablePath, string[] arguments, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutablePath = executablePath;
            Calls.Add([.. arguments]);
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No scripted herdr response is left for " + string.Join(' ', arguments));
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
