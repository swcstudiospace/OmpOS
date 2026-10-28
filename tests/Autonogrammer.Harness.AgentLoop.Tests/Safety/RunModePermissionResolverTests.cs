using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>The resolver composes the run mode with the existing approval policy and the user: it can deny, allow within the ceiling, or ask, and never grants past the ceiling.</summary>
public sealed class RunModePermissionResolverTests
{
    private static PermissionRequestEvent Request(string kind, string rawInput, string callId = "c1", string title = "tool") =>
        new(callId, title, "{\"toolCall\":{\"toolCallId\":\"" + callId + "\",\"title\":\"" + title + "\",\"kind\":\"" + kind + "\",\"rawInput\":" + rawInput + "}}", "9");

    private static PermissionRequestEvent Shell(string command) => Request("execute", "{\"command\":\"" + command + "\"}");

    [Fact]
    public async Task ADeniedCallIsDeniedAndTheUserIsNeverAsked()
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "permissions": { "deny": ["Shell(rm)"] } }""");
        var user = new FakeUser(PermissionDecision.Allow);
        var resolver = new RunModePermissionResolver(user, () => policy);

        var decision = await resolver.ResolveAsync(Shell("rm -rf x"), TestContext.Current.CancellationToken).ConfigureAwait(true);

        decision.ShouldBe(PermissionDecision.Deny);
        user.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnAllowedCallIsAllowedWithoutAskingTheUser()
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "terminalAllowlist": ["git status"] }""");
        var user = new FakeUser(PermissionDecision.Deny);
        var resolver = new RunModePermissionResolver(user, () => policy);

        var decision = await resolver.ResolveAsync(Shell("git status"), TestContext.Current.CancellationToken).ConfigureAwait(true);

        decision.ShouldBe(PermissionDecision.Allow);
        user.Asked.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(PermissionDecision.Allow)]
    [InlineData(PermissionDecision.Deny)]
    public async Task ACallThePolicyCannotSettleIsPassedToTheUserAndTheUsersAnswerStands(PermissionDecision answer)
    {
        using var rig = new SafetyRig();
        var policy = rig.Policy(RunMode.Allowlist);
        var user = new FakeUser(answer);
        var resolver = new RunModePermissionResolver(user, () => policy);
        var request = Shell("make all");

        var decision = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        decision.ShouldBe(answer);
        user.Asked.ShouldBe([request]);
    }

    [Fact]
    public async Task TheUsersWaitIsCancelledWithTheCallersToken()
    {
        using var rig = new SafetyRig();
        var policy = rig.Policy(RunMode.Allowlist);
        var user = new FakeUser(PermissionDecision.Allow) { Hold = true };
        var resolver = new RunModePermissionResolver(user, () => policy);
        using var cancel = new CancellationTokenSource();

        var pending = resolver.ResolveAsync(Shell("make"), cancel.Token);
        await cancel.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(pending).ConfigureAwait(true);
    }

    [Fact]
    public async Task ARunModeNeverGrantsACallTheExistingPolicyForbids()
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "terminalAllowlist": ["git"] }""");
        var user = new FakeUser(PermissionDecision.Allow);
        var seen = new List<PermissionRequestEvent>();
        var resolver = new RunModePermissionResolver(user, () => policy, ceiling: request =>
        {
            seen.Add(request);
            return false;
        });

        var decision = await resolver.ResolveAsync(Shell("git status"), TestContext.Current.CancellationToken).ConfigureAwait(true);

        decision.ShouldBe(PermissionDecision.Deny, "the allowlist matches, but the approval policy does not admit the call");
        seen.Count.ShouldBe(1);
        user.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACeilingThatAdmitsTheCallLeavesTheDecisionToThePolicy()
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "terminalAllowlist": ["git"] }""");
        var resolver = new RunModePermissionResolver(new FakeUser(PermissionDecision.Deny), () => policy, ceiling: _ => true);

        (await resolver.ResolveAsync(Shell("git status"), TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(PermissionDecision.Allow);
    }

    [Fact]
    public async Task RunEverythingIsTheOneModeThatDoesNotConsultTheCeiling()
    {
        using var rig = new SafetyRig();
        var policy = rig.Policy(RunMode.RunEverything);
        var resolver = new RunModePermissionResolver(new FakeUser(PermissionDecision.Deny), () => policy, ceiling: _ => false);

        (await resolver.ResolveAsync(Shell("anything"), TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(PermissionDecision.Allow);
    }

    [Fact]
    public async Task ADenyRuleStillAppliesInRunEverything()
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.RunEverything, """{ "permissions": { "deny": ["Shell(curl)"] } }""");
        var resolver = new RunModePermissionResolver(new FakeUser(PermissionDecision.Allow), () => policy);

        (await resolver.ResolveAsync(Shell("curl https://x"), TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(PermissionDecision.Deny);
    }

    [Fact]
    public async Task ThePolicyIsFetchedForEveryRequestSoAModeChangeTakesEffect()
    {
        using var rig = new SafetyRig();
        var current = rig.Policy(RunMode.Allowlist);
        var resolver = new RunModePermissionResolver(new FakeUser(PermissionDecision.Deny), () => current);

        (await resolver.ResolveAsync(Shell("make"), TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(PermissionDecision.Deny);
        current = rig.Policy(RunMode.RunEverything);
        (await resolver.ResolveAsync(Shell("make"), TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(PermissionDecision.Allow);
    }

    [Fact]
    public async Task EveryDecisionIsRecordedWithTheRuleOrStepThatSettledIt()
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "terminalAllowlist": ["git status"], "permissions": { "deny": ["Shell(rm)"] } }""");
        var log = new RunModeAuditLog(capacity: 2);
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));
        var resolver = new RunModePermissionResolver(new FakeUser(PermissionDecision.Deny), () => policy, audit: log, clock: clock);
        var ct = TestContext.Current.CancellationToken;

        await resolver.ResolveAsync(Request("execute", "{\"command\":\"git status\"}", "a", "Run git"), ct).ConfigureAwait(true);
        await resolver.ResolveAsync(Request("execute", "{\"command\":\"rm x\"}", "b"), ct).ConfigureAwait(true);
        await resolver.ResolveAsync(Request("read", "{\"path\":\"src/a.ts\"}", "c"), ct).ConfigureAwait(true);

        var entries = log.Snapshot();
        entries.Count.ShouldBe(2, "the log keeps its newest entries only");
        entries[0].CallId.ShouldBe("b");
        entries[0].Decision.Outcome.ShouldBe(RunModeOutcome.Deny);
        entries[0].Decision.Stage.ShouldBe(RunModeStage.DenyRule);
        entries[0].Subject.ShouldBe("rm x");
        entries[0].At.ShouldBe(clock.GetUtcNow());
        entries[1].Decision.Stage.ShouldBe(RunModeStage.ReadBoundary);
        entries[1].Kind.ShouldBe(RunModeToolKind.Read);
        entries[1].Subject.ShouldBe("src/a.ts");
    }

    [Fact]
    public async Task ACeilingDenialIsRecordedAndALongSubjectIsCut()
    {
        using var rig = new SafetyRig();
        var log = new RunModeAuditLog();
        var policy = rig.Policy(RunMode.Allowlist);
        var resolver = new RunModePermissionResolver(new FakeUser(PermissionDecision.Allow), () => policy, ceiling: _ => false, audit: log);

        await resolver.ResolveAsync(Shell(new string('x', 500)), TestContext.Current.CancellationToken).ConfigureAwait(true);

        var entry = log.Snapshot().Single();
        entry.Decision.Stage.ShouldBe(RunModeStage.PolicyCeiling);
        entry.Subject.Length.ShouldBe(200);
    }

    [Fact]
    public void TheConstructorAndTheLogRejectWhatTheyCannotUse()
    {
        using var rig = new SafetyRig();

        Should.Throw<ArgumentNullException>(() => new RunModePermissionResolver(null!, () => rig.Policy(RunMode.Allowlist)));
        Should.Throw<ArgumentNullException>(() => new RunModePermissionResolver(new FakeUser(PermissionDecision.Deny), null!));
        Should.Throw<ArgumentOutOfRangeException>(() => new RunModeAuditLog(0));
        Should.Throw<ArgumentNullException>(() => new RunModeAuditLog().Record(null!));
    }

    private sealed class FakeUser(PermissionDecision answer) : IPermissionResolver
    {
        public List<PermissionRequestEvent> Asked { get; } = [];

        public bool Hold { get; init; }

        public async Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            Asked.Add(request);
            if (Hold)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }

            return answer;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
