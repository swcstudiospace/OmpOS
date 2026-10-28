using System.Net;
using System.Text;
using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.Controls;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Web.Demo;
using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>The Demo handler keeps the real API's contract: scripted streaming at the typing cadence, state that mutating calls change, and the same problem statuses.</summary>
public sealed class DemoApiHandlerTests : IDisposable
{
    private readonly DemoTestHost _host = new();

    /// <summary>A prompt starts a scripted reply that arrives chunk by chunk as the clock advances, ends with completed, and moves the session to Completed.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task PromptStreamsTheReplyIncrementallyThenCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _host.Api.CreateSessionAsync(ct).ConfigureAwait(true);
        await _host.Api.SubmitPromptAsync(created.Id, "hello", ct).ConfigureAwait(true);
        var log = new EventLog();
        var collecting = CollectAsync(created.Id, log, ct);

        await _host.PumpUntilAsync(() => log.Snapshot().Count > 0).ConfigureAwait(true);
        log.Snapshot()[0].Payload.ShouldBeOfType<UnknownPayload>().OriginalKind.ShouldBe("prompt_accepted");
        log.Snapshot().Count(static evt => evt.Payload is TextDeltaPayload).ShouldBe(0, "no text before the first typing delay has passed");

        _host.Time.Advance(TimeSpan.FromMilliseconds(35));
        await _host.PumpUntilAsync(() => log.Snapshot().Any(static evt => evt.Payload is TextDeltaPayload), TimeSpan.Zero).ConfigureAwait(true);
        log.Snapshot().Count(static evt => evt.Payload is CompletedPayload).ShouldBe(0, "the reply is still typing");

        await _host.PumpUntilAsync(() => collecting.IsCompleted).ConfigureAwait(true);
        await collecting.ConfigureAwait(true);
        var events = log.Snapshot();
        events[^1].Payload.ShouldBeOfType<CompletedPayload>();
        events.Select(static evt => evt.Seq).ShouldBe(events.Select(static evt => evt.Seq).Order().ToList());
        _host.World.Find(created.Id).Status.ShouldBe(SessionStatus.Completed);
        var entry = (await _host.Api.ListSessionsAsync(ct).ConfigureAwait(true)).Single(candidate => string.Equals(candidate.Id, created.Id, StringComparison.Ordinal));
        entry.RowStatus.ShouldBe(CatalogRowStatus.Completed);
        entry.Messages.Single().Content.ShouldBe("hello");
    }

    /// <summary>The session stream resumes after a position, replays only later events and ends at the terminal event.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task SessionStreamResumesAfterAPosition()
    {
        var log = new EventLog();

        await CollectAsync(DemoFixtures.CompletedId, log, TestContext.Current.CancellationToken, since: 6).ConfigureAwait(true);

        var events = log.Snapshot();
        events.Select(static evt => evt.Seq).ShouldBe(new long[] { 6, 7 });
        events[^1].Payload.ShouldBeOfType<CompletedPayload>();
    }

    /// <summary>The problem statuses match the real API: a running turn blocks a prompt, a closed session refuses one, a terminal takes none.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task PromptRefusalsUseTheRealStatuses()
    {
        var ct = TestContext.Current.CancellationToken;
        var running = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.SubmitPromptAsync(DemoFixtures.ApprovalId, "x", ct)).ConfigureAwait(true);
        var closed = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.SubmitPromptAsync(DemoFixtures.ClosedId, "x", ct)).ConfigureAwait(true);
        var terminal = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.SubmitPromptAsync(DemoFixtures.TerminalId, "x", ct)).ConfigureAwait(true);
        var missing = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.GetSessionAsync("nope", ct)).ConfigureAwait(true);

        running.Kind.ShouldBe(ApiProblemKind.Conflict);
        closed.Kind.ShouldBe(ApiProblemKind.Conflict);
        closed.Code.ShouldBe("session_closed");
        terminal.Code.ShouldBe("session_not_promptable");
        missing.Kind.ShouldBe(ApiProblemKind.NotFound);
    }

    /// <summary>Wrong media type is 415 and a body that is not JSON is 400, like the real API.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task BodyChecksAnswer415And400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var wrongType = new StringContent("{}", Encoding.UTF8, "text/plain");
        using var notJson = new StringContent("{nope", Encoding.UTF8, "application/json");
        using var first = await _host.Http.PostAsync(new Uri("sessions/" + DemoFixtures.CompletedId + "/prompts", UriKind.Relative), wrongType, ct).ConfigureAwait(true);
        using var second = await _host.Http.PostAsync(new Uri("sessions/" + DemoFixtures.CompletedId + "/prompts", UriKind.Relative), notJson, ct).ConfigureAwait(true);

        first.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>A route the Demo does not have is a 404 problem, never a hang.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task UnknownRouteIsA404Problem()
    {
        using var response = await _host.Http.GetAsync(new Uri("nothing-here", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Steering is delivered while a turn runs, held while it waits for an approval, and refused with 409 when nothing runs.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task SteeringFollowsTheStateOfTheTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _host.Api.CreateSessionAsync(ct).ConfigureAwait(true);
        var idle = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.SteerAsync(created.Id, "x", SteerMode.SendNow, ct)).ConfigureAwait(true);
        await _host.Api.SubmitPromptAsync(created.Id, "go", ct).ConfigureAwait(true);

        var delivered = await _host.Api.SteerAsync(created.Id, "smaller", SteerMode.SendNow, ct).ConfigureAwait(true);
        var queued = await _host.Api.SteerAsync(created.Id, "later", SteerMode.Queue, ct).ConfigureAwait(true);
        var held = await _host.Api.SteerAsync(DemoFixtures.ApprovalId, "wait", SteerMode.SendNow, ct).ConfigureAwait(true);

        idle.Code.ShouldBe("steering_unavailable");
        delivered.Delivery.ShouldBe(SteerDelivery.Delivered);
        queued.Delivery.ShouldBe(SteerDelivery.Queued);
        held.Delivery.ShouldBe(SteerDelivery.Held);
        var log = _host.World.Find(created.Id).Events.Select(static evt => evt.Payload).OfType<UnknownPayload>().Select(static payload => payload.Raw.ToString()).ToList();
        log.ShouldNotContain(raw => raw.Contains("smaller", StringComparison.Ordinal), "the log keeps the length of a steering message, never its text");
    }

    /// <summary>Denying a tool request ends the turn with the scripted refusal and clears the pending list; a second decision is a conflict.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task DenyingAToolRequestEndsTheTurn()
    {
        var ct = TestContext.Current.CancellationToken;

        await _host.Api.DecideToolApprovalAsync(DemoFixtures.ApprovalId, DemoFixtures.ApprovalRequestId, allow: false, ct).ConfigureAwait(true);
        await _host.PumpUntilAsync(() => _host.World.Find(DemoFixtures.ApprovalId).Status == SessionStatus.Completed).ConfigureAwait(true);

        (await _host.Api.GetToolApprovalsAsync(DemoFixtures.ApprovalId, ct).ConfigureAwait(true)).Approvals.ShouldBeEmpty();
        var again = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.DecideToolApprovalAsync(DemoFixtures.ApprovalId, DemoFixtures.ApprovalRequestId, allow: true, ct)).ConfigureAwait(true);
        again.Kind.ShouldBe(ApiProblemKind.Conflict);
        _host.World.Find(DemoFixtures.ApprovalId).Events.Select(static evt => evt.Payload).OfType<UnknownPayload>().Any(static payload => string.Equals(payload.OriginalKind, "tool_approval_denied", StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>An option changes between runs; an unknown option, a value the engine does not offer, a running turn and a closed session are 404, 400, 409 and 409.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task OptionChangesFollowTheRealRules()
    {
        var ct = TestContext.Current.CancellationToken;

        var changed = await _host.Api.SetConfigOptionAsync(DemoFixtures.CompletedId, "thinking", "high", ct).ConfigureAwait(true);
        var unknown = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.SetConfigOptionAsync(DemoFixtures.CompletedId, "nope", "x", ct)).ConfigureAwait(true);
        var invalid = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.SetConfigOptionAsync(DemoFixtures.CompletedId, "mode", "turbo", ct)).ConfigureAwait(true);
        var busy = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.SetConfigOptionAsync(DemoFixtures.ApprovalId, "mode", "plan", ct)).ConfigureAwait(true);
        var closed = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.SetConfigOptionAsync(DemoFixtures.ClosedId, "mode", "plan", ct)).ConfigureAwait(true);

        changed.Options.Single(static option => string.Equals(option.Id, "thinking", StringComparison.Ordinal)).CurrentValue.ShouldBe("high");
        unknown.Code.ShouldBe("config_option_not_found");
        unknown.Kind.ShouldBe(ApiProblemKind.NotFound);
        invalid.Kind.ShouldBe(ApiProblemKind.BadRequest);
        busy.Code.ShouldBe("config_option_busy");
        closed.Code.ShouldBe("config_options_unavailable");
    }

    /// <summary>Accepting and rejecting changes is recorded in memory with an honest note that no file was written; an unknown path is 404.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ChangeDecisionsAreRecordedAndLabelled()
    {
        var ct = TestContext.Current.CancellationToken;

        var accepted = await _host.Api.DecideChangeAsync(DemoFixtures.CompletedId, "src/Products/ProductsController.cs", accept: true, ct).ConfigureAwait(true);
        var missing = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.DecideChangeAsync(DemoFixtures.CompletedId, "not/a/change.cs", accept: true, ct)).ConfigureAwait(true);
        var listed = await _host.Api.GetChangesAsync(DemoFixtures.CompletedId, ct).ConfigureAwait(true);

        accepted.Status.ShouldBe(ChangeReviewStatus.Accepted);
        accepted.Applied.ShouldBeFalse();
        accepted.Reason.ShouldNotBeNull().ShouldContain("Demo");
        missing.Kind.ShouldBe(ApiProblemKind.NotFound);
        listed.Changes.Count.ShouldBe(3);
        listed.Changes.Count(static change => change.Kind == ChangeKind.Added).ShouldBe(2);
        listed.Changes.Single(static change => change.Path.EndsWith("Controller.cs", StringComparison.Ordinal)).Status.ShouldBe(ChangeReviewStatus.Accepted);
    }

    /// <summary>Close cancels a running turn; reopen needs a closed session; resume needs a lost one (the Demo has none).</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task LifecycleCallsFollowTheRealRules()
    {
        var ct = TestContext.Current.CancellationToken;
        var notClosed = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.ReopenSessionAsync(DemoFixtures.CompletedId, ct)).ConfigureAwait(true);
        var notLost = await Should.ThrowAsync<ApiProblemException>(() => _host.Api.ResumeSessionAsync(DemoFixtures.CompletedId, ct)).ConfigureAwait(true);
        var created = await _host.Api.CreateSessionAsync(ct).ConfigureAwait(true);
        await _host.Api.SubmitPromptAsync(created.Id, "run", ct).ConfigureAwait(true);

        var closed = await _host.Api.CloseSessionAsync(created.Id, ct).ConfigureAwait(true);
        _host.Time.Advance(TimeSpan.FromMinutes(1));

        notClosed.Code.ShouldBe("session_not_closed");
        notLost.Code.ShouldBe("session_not_lost");
        closed.Closed.ShouldBeTrue();
        closed.RowStatus.ShouldBe(CatalogRowStatus.Closed);
        _host.World.Find(created.Id).Status.ShouldBe(SessionStatus.Cancelled);
        _host.World.Find(created.Id).RunInFlight.ShouldBeFalse();
    }

    /// <summary>The user stream resumes after a known position and answers an unknown position with one resync event.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task UserStreamResumesAndResyncs()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _host.World.Create();
        _ = _host.World.Close(DemoFixtures.FailedId);
        using var resumed = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var unknown = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var replay = await FirstAsync(_host.Feed.StreamAsync(1, resumed.Token), resumed).ConfigureAwait(true);
        var resync = await FirstAsync(_host.Feed.StreamAsync(999_999, unknown.Token), unknown).ConfigureAwait(true);

        replay.Id.ShouldBe(2);
        replay.Catalog.ShouldNotBeNull().Change.ShouldBe(CatalogChangeKind.Closed);
        resync.Kind.ShouldBe("resync");
        resync.Id.ShouldBeNull();
    }

    /// <summary>The scripted terminal and the settings of the Demo exist only in the Demo: the production exclusion is covered by <see cref="DemoExclusionTests"/>.</summary>
    [Fact]
    public void FixturesCoverEveryStateTheTaskNames()
    {
        var entries = _host.World.ListEntries();

        entries.Count.ShouldBe(6);
        entries.Count(static entry => entry.Kind == CatalogSessionKind.Terminal).ShouldBe(1);
        entries.Count(static entry => entry.Closed).ShouldBe(1);
        entries.Single(static entry => string.Equals(entry.Id, DemoFixtures.FailedId, StringComparison.Ordinal)).Events.Select(static evt => evt.Payload).OfType<ErrorPayload>().Single().Retryable.ShouldBeTrue();
        entries.Single(static entry => string.Equals(entry.Id, DemoFixtures.CompletedId, StringComparison.Ordinal)).Events.Select(static evt => evt.Payload).OfType<UiBlockPayload>().Count().ShouldBe(3);
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    private static async Task<UserStreamEvent> FirstAsync(IAsyncEnumerable<UserStreamEvent> stream, CancellationTokenSource cancellation)
    {
        await foreach (var evt in stream.ConfigureAwait(true))
        {
            await cancellation.CancelAsync().ConfigureAwait(true);
            return evt;
        }

        throw new InvalidOperationException("The stream ended without an event.");
    }

    private async Task CollectAsync(string sessionId, EventLog into, CancellationToken cancellationToken, long? since = null)
    {
        await foreach (var evt in _host.Api.StreamSessionAsync(sessionId, since, cancellationToken).ConfigureAwait(true))
        {
            into.Add(evt);
        }
    }

    private sealed class EventLog
    {
        private readonly List<StreamEvent> _events = [];

        public void Add(StreamEvent evt)
        {
            lock (_events)
            {
                _events.Add(evt);
            }
        }

        public IReadOnlyList<StreamEvent> Snapshot()
        {
            lock (_events)
            {
                return [.. _events];
            }
        }
    }
}
