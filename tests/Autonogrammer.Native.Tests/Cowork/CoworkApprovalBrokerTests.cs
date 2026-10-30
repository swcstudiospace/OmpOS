using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Tools;
using Autonogrammer.Native.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>Cowork approvals use the host timeout and refuse a remote decision for a local-only ask.</summary>
public sealed class CoworkApprovalBrokerTests
{
    [Fact]
    public async Task TimeoutUsesTheHostOptionAndSaysNotApproved()
    {
        var broker = new CoworkApprovalBroker(new CoworkToolHostOptions { ApprovalTimeout = TimeSpan.FromMilliseconds(30) });
        var decision = await broker.RequestAsync(Ask(mustBeLocal: true), TestContext.Current.CancellationToken);
        decision.Outcome.ShouldBe(CoworkApprovalOutcome.TimedOut);
        broker.Cards.ShouldHaveSingleItem().StatusText.ShouldBe("Timed out");
        broker.ApprovalTimeout.ShouldBe(TimeSpan.FromMilliseconds(30));
    }

    [Fact]
    public async Task NoWindowIsUnavailable()
    {
        var broker = new CoworkApprovalBroker(windowAvailable: false);
        var decision = await broker.RequestAsync(Ask(true), TestContext.Current.CancellationToken);
        decision.Outcome.ShouldBe(CoworkApprovalOutcome.Unavailable);
        broker.Cards.ShouldHaveSingleItem().StatusText.ShouldBe("Unavailable");
    }

    [Fact]
    public async Task ARemoteDecisionDoesNotCompleteMustBeLocal()
    {
        var broker = new CoworkApprovalBroker(new CoworkToolHostOptions { ApprovalTimeout = TimeSpan.FromSeconds(30) });
        var waiting = broker.RequestAsync(Ask(mustBeLocal: true), TestContext.Current.CancellationToken);
        broker.CompleteFromRemote("call-1", CoworkApprovalOutcome.Allowed).ShouldBeFalse();
        broker.CompleteLocal("call-1", CoworkApprovalOutcome.Denied).ShouldBeTrue();
        (await waiting).Outcome.ShouldBe(CoworkApprovalOutcome.Denied);
    }

    private static CoworkApprovalRequest Ask(bool mustBeLocal) =>
        new("session", "call-1", "mcp__cowork_artifact_create", new CoworkCallPreview("Create", "/tmp/out", ["host"], null), CoworkTurnOrigin.Local, mustBeLocal);
}
