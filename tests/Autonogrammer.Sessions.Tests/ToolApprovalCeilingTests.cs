using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Tests;

/// <summary>The policy ceiling of a tool approval: named tools, the <c>*</c> wildcard, and what the stored request keeps.</summary>
public sealed class ToolApprovalCeilingTests
{
    private const string RequestId = "req-1";
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A ceiling that holds the wildcard lets a decision allow any tool, and the stored ceiling stays the configured list.</summary>
    [Fact]
    public async Task WildcardCeilingPermitsAnyTool()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = new SessionService(repository, new FixedTimeProvider(Now));
        var session = await RunningSessionAsync(service, ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, RequestId, "edit_file", ct, Ceiling(SessionService.AnyToolCeiling)).ConfigureAwait(true);

        var stored = await repository.GetToolApprovalAsync(session.Id, RequestId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull().GrantableTools.ShouldBe(["*"]);

        await service.ResolveToolApprovalAsync(session.Id, RequestId, approved: true, ct).ConfigureAwait(true);

        var decided = await repository.GetToolApprovalAsync(session.Id, RequestId, ct).ConfigureAwait(true);
        decided.ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Allow);
        decided.GrantableTools.ShouldBe(["*"]);
    }

    /// <summary>A ceiling that names other tools, without the wildcard, still refuses a tool it does not name; the request stays pending.</summary>
    [Fact]
    public async Task NamedCeilingWithoutWildcardRefusesOtherTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = new SessionService(repository, new FixedTimeProvider(Now));
        var session = await RunningSessionAsync(service, ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, RequestId, "bash", ct, Ceiling("edit_file")).ConfigureAwait(true);

        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            service.ResolveToolApprovalAsync(session.Id, RequestId, approved: true, ct)).ConfigureAwait(true);

        var stored = await repository.GetToolApprovalAsync(session.Id, RequestId, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull().Decision.ShouldBeNull();
    }

    /// <summary>A ceiling that names the tool permits it, and the match is exact: a different case is refused.</summary>
    [Fact]
    public async Task NamedCeilingPermitsOnlyTheExactTool()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = new SessionService(repository, new FixedTimeProvider(Now));
        var session = await RunningSessionAsync(service, ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, RequestId, "bash", ct, Ceiling("bash")).ConfigureAwait(true);
        await service.ResolveToolApprovalAsync(session.Id, RequestId, approved: true, ct).ConfigureAwait(true);
        (await repository.GetToolApprovalAsync(session.Id, RequestId, ct).ConfigureAwait(true)).ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Allow);

        const string second = "req-2";
        await service.RequestToolApprovalAsync(session.Id, second, "Bash", ct, Ceiling("bash")).ConfigureAwait(true);
        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            service.ResolveToolApprovalAsync(session.Id, second, approved: true, ct)).ConfigureAwait(true);
    }

    /// <summary>A deny is accepted whatever the ceiling holds, including the empty ceiling.</summary>
    [Fact]
    public async Task DenyIsAlwaysAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new InMemorySessionRepository();
        var service = new SessionService(repository, new FixedTimeProvider(Now));
        var session = await RunningSessionAsync(service, ct).ConfigureAwait(true);
        await service.RequestToolApprovalAsync(session.Id, RequestId, "bash", ct, Ceiling()).ConfigureAwait(true);

        await service.ResolveToolApprovalAsync(session.Id, RequestId, approved: false, ct).ConfigureAwait(true);

        (await repository.GetToolApprovalAsync(session.Id, RequestId, ct).ConfigureAwait(true)).ShouldNotBeNull().Decision.ShouldBe(PermissionDecision.Deny);
    }

    private static HashSet<string> Ceiling(params string[] tools) => new(tools, StringComparer.Ordinal);

    private static async Task<Session> RunningSessionAsync(SessionService service, CancellationToken ct)
    {
        var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
        await service.SubmitPromptAsync(session.Id, "run", ct).ConfigureAwait(true);
        return session;
    }
}
