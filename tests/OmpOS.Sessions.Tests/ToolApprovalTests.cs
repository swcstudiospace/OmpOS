using OmpOS.Contracts.Errors;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Sessions.Tests;

/// <summary>Tool approval of <see cref="SessionService"/>: request ids, the policy ceiling, exactly-once decisions and the timeout denial.</summary>
public sealed class ToolApprovalTests
{
    private const string RequestId = "req-1";
    private const string Tool = "Bash";
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlySet<string> GrantBash = new HashSet<string>(StringComparer.Ordinal) { Tool };
    private static readonly TimeSpan LongWait = TimeSpan.FromSeconds(10);

    /// <summary>The default decision is deny, so a request that reaches no grant is refused.</summary>
    [Fact]
    public void DefaultDecisionIsDeny()
    {
        default(PermissionDecision).ShouldBe(PermissionDecision.Deny);
    }

    /// <summary>A decision reaches the waiter, resumes the prompt, and is recorded with its request id; the request is decided once.</summary>
    [Fact]
    public async Task AllowedDecisionReachesWaiterResumesPromptAndIsDecidedOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await RunningSessionAsync(service, ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, RequestId, Tool, ct, GrantBash).ConfigureAwait(true);

        var waiting = service.WaitForDecisionAsync(session.Id, RequestId, LongWait, ct);
        await service.ResolveToolApprovalAsync(session.Id, RequestId, approved: true, ct).ConfigureAwait(true);

        (await waiting.ConfigureAwait(true)).ShouldBe(PermissionDecision.Allow);
        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Running);
        EventKinds(stored).ShouldContain("tool_approval_requested", StringComparer.Ordinal);
        EventKinds(stored).ShouldContain("tool_approval_approved", StringComparer.Ordinal);
        RequestedEvent(stored).GetProperty("requestId").GetString().ShouldBe(RequestId);

        var again = await Should.ThrowAsync<SessionException>(() =>
            service.ResolveToolApprovalAsync(session.Id, RequestId, approved: true, ct)).ConfigureAwait(true);
        again.Error.Code.ShouldBe(ErrorCode.InvalidTransition);
    }

    /// <summary>With the default ceiling nothing is grantable: an allow is refused and the request stays pending for a denial.</summary>
    [Fact]
    public async Task AllowAboveDefaultCeilingIsRefusedAndRequestStaysPending()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await RunningSessionAsync(service, ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, RequestId, Tool, ct).ConfigureAwait(true);

        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            service.ResolveToolApprovalAsync(session.Id, RequestId, approved: true, ct)).ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.AwaitingToolApproval);

        await service.ResolveToolApprovalAsync(session.Id, RequestId, approved: false, ct).ConfigureAwait(true);
        var resumed = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        resumed.ShouldNotBeNull();
        resumed.Status.ShouldBe(SessionStatus.Running);
        EventKinds(resumed).ShouldContain("tool_approval_denied", StringComparer.Ordinal);
    }

    /// <summary>A denial after a denial, or an allow after a denial, is refused: the request was decided once.</summary>
    [Fact]
    public async Task DecisionAfterDenialIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NewService(new InMemorySessionRepository());
        var session = await RunningSessionAsync(service, ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, RequestId, Tool, ct).ConfigureAwait(true);
        await service.ResolveToolApprovalAsync(session.Id, RequestId, approved: false, ct).ConfigureAwait(true);

        var repeat = await Should.ThrowAsync<SessionException>(() =>
            service.ResolveToolApprovalAsync(session.Id, RequestId, approved: false, ct)).ConfigureAwait(true);
        repeat.Error.Code.ShouldBe(ErrorCode.InvalidTransition);

        var allow = await Should.ThrowAsync<SessionException>(() =>
            service.ResolveToolApprovalAsync(session.Id, RequestId, approved: true, ct)).ConfigureAwait(true);
        allow.Error.Code.ShouldBe(ErrorCode.InvalidTransition);
    }

    /// <summary>A decision for an id that was never requested is refused while the session awaits approval.</summary>
    [Fact]
    public async Task UnknownRequestIdIsRefusedWhileAwaitingApproval()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await RunningSessionAsync(service, ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, RequestId, Tool, ct, GrantBash).ConfigureAwait(true);

        var error = await Should.ThrowAsync<SessionException>(() =>
            service.ResolveToolApprovalAsync(session.Id, "absent", approved: true, ct)).ConfigureAwait(true);

        error.Error.Code.ShouldBe(ErrorCode.InvalidTransition);
        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.AwaitingToolApproval);
    }

    /// <summary>A request needs a running prompt, and a decision needs a session that awaits approval.</summary>
    [Fact]
    public async Task RequestNeedsRunningPromptAndDecisionNeedsAwaitingSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NewService(new InMemorySessionRepository());
        var idle = await service.CreateAsync("dev", ct).ConfigureAwait(true);

        var request = await Should.ThrowAsync<SessionException>(() =>
            service.RequestToolApprovalAsync(idle.Id, RequestId, Tool, ct)).ConfigureAwait(true);
        request.Error.Code.ShouldBe(ErrorCode.InvalidTransition);

        var resolve = await Should.ThrowAsync<SessionException>(() =>
            service.ResolveToolApprovalAsync(idle.Id, RequestId, approved: false, ct)).ConfigureAwait(true);
        resolve.Error.Code.ShouldBe(ErrorCode.InvalidTransition);
    }

    /// <summary>A decision for a missing session reports the session as not found.</summary>
    [Fact]
    public async Task DecisionForMissingSessionIsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NewService(new InMemorySessionRepository());

        var error = await Should.ThrowAsync<SessionException>(() =>
            service.ResolveToolApprovalAsync("absent", RequestId, approved: false, ct)).ConfigureAwait(true);

        error.Error.Code.ShouldBe(ErrorCode.SessionNotFound);
    }

    /// <summary>A request that no decision reaches within the timeout is denied, and the session resumes its prompt.</summary>
    [Fact]
    public async Task RequestWithoutDecisionIsDeniedWhenTimeoutElapses()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await RunningSessionAsync(service, ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, RequestId, Tool, ct, GrantBash).ConfigureAwait(true);

        var decision = await service.WaitForDecisionAsync(session.Id, RequestId, TimeSpan.FromMilliseconds(20), ct).ConfigureAwait(true);

        decision.ShouldBe(PermissionDecision.Deny);
        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Running);
        EventKinds(stored).ShouldContain("tool_approval_denied", StringComparer.Ordinal);

        var late = await Should.ThrowAsync<SessionException>(() =>
            service.ResolveToolApprovalAsync(session.Id, RequestId, approved: true, ct)).ConfigureAwait(true);
        late.Error.Code.ShouldBe(ErrorCode.InvalidTransition);
    }

    /// <summary>A cancelled wait denies the pending request before the cancellation propagates, so the session is never left awaiting.</summary>
    [Fact]
    public async Task CancelledWaitDeniesRequestBeforeCancellationPropagates()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = NewService(repository);
        var session = await RunningSessionAsync(service, ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, RequestId, Tool, ct).ConfigureAwait(true);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.WaitForDecisionAsync(session.Id, RequestId, LongWait, cancelled.Token)).ConfigureAwait(true);

        var stored = await repository.GetAsync(session.Id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Running);
        EventKinds(stored).ShouldContain("tool_approval_denied", StringComparer.Ordinal);
    }

    /// <summary>A wait for a request that is not pending is refused.</summary>
    [Fact]
    public async Task WaitForRequestThatIsNotPendingIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = NewService(new InMemorySessionRepository());
        var session = await RunningSessionAsync(service, ct).ConfigureAwait(true);

        var error = await Should.ThrowAsync<SessionException>(() =>
            service.WaitForDecisionAsync(session.Id, RequestId, LongWait, ct)).ConfigureAwait(true);

        error.Error.Code.ShouldBe(ErrorCode.InvalidTransition);
    }

    private static SessionService NewService(ISessionRepository repository) =>
        new(repository, new FixedTimeProvider(Now));

    private static async Task<Session> RunningSessionAsync(SessionService service, CancellationToken ct)
    {
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "run", ct).ConfigureAwait(true);
        return session;
    }

    private static string?[] EventKinds(Session session) =>
        session.Events
            .Select(static evt => evt.Payload)
            .OfType<UnknownPayload>()
            .Select(static payload => payload.OriginalKind)
            .ToArray();

    private static System.Text.Json.JsonElement RequestedEvent(Session session) =>
        session.Events
            .Select(static evt => evt.Payload)
            .OfType<UnknownPayload>()
            .Single(static payload => string.Equals(payload.OriginalKind, "tool_approval_requested", StringComparison.Ordinal))
            .Raw;
}
