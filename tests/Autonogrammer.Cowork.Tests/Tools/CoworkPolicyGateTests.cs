using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Tools;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Tools;

/// <summary>Default gate rules for mode, grants, taint and ByProviderGate.</summary>
public sealed class CoworkPolicyGateTests
{
    [Fact]
    public async Task ByProviderGate_ReturnsAllowAndNeverAsks()
    {
        var gate = new CoworkPolicyGate(new Store(), () => CoworkPermissionMode.Manual);
        var decision = await gate.DecideAsync(Request(CoworkApprovalRequirement.ByProviderGate, CoworkToolEffect.Actuating, tainted: true), TestContext.Current.CancellationToken);
        decision.Outcome.ShouldBe(CoworkPolicyOutcome.Allow);
        decision.MustBeLocal.ShouldBeFalse();
    }

    [Fact]
    public async Task EveryCallOnExecutingDevice_AsksLocallyInSkip()
    {
        var gate = new CoworkPolicyGate(new Store(), () => CoworkPermissionMode.Skip);
        var decision = await gate.DecideAsync(Request(CoworkApprovalRequirement.EveryCallOnExecutingDevice, CoworkToolEffect.Actuating, tainted: false), TestContext.Current.CancellationToken);
        decision.Outcome.ShouldBe(CoworkPolicyOutcome.Ask);
        decision.MustBeLocal.ShouldBeTrue();
    }

    [Fact]
    public async Task StoredGrant_SatisfiesByPolicyManualAndNotDestructiveOrEveryCall()
    {
        var store = new Store();
        await store.GrantAsync(new CoworkGrantKey("artifact.write", "cowork"), TestContext.Current.CancellationToken);
        var gate = new CoworkPolicyGate(store, () => CoworkPermissionMode.Manual);
        var allowed = await gate.DecideAsync(Request(CoworkApprovalRequirement.ByPolicy, CoworkToolEffect.Actuating, tainted: false, grant: true), TestContext.Current.CancellationToken);
        allowed.Outcome.ShouldBe(CoworkPolicyOutcome.Allow);
        var destructive = await gate.DecideAsync(Request(CoworkApprovalRequirement.ByPolicy, CoworkToolEffect.Destructive, tainted: false, grant: true), TestContext.Current.CancellationToken);
        destructive.Outcome.ShouldBe(CoworkPolicyOutcome.Ask);
        var every = await gate.DecideAsync(Request(CoworkApprovalRequirement.EveryCall, CoworkToolEffect.Actuating, tainted: false, grant: true), TestContext.Current.CancellationToken);
        every.Outcome.ShouldBe(CoworkPolicyOutcome.Ask);
    }

    [Fact]
    public async Task TaintedSession_AsksDespiteGrant()
    {
        var store = new Store();
        await store.GrantAsync(new CoworkGrantKey("artifact.write", "cowork"), TestContext.Current.CancellationToken);
        var gate = new CoworkPolicyGate(store, () => CoworkPermissionMode.Skip);
        var decision = await gate.DecideAsync(Request(CoworkApprovalRequirement.ByPolicy, CoworkToolEffect.Actuating, tainted: true, grant: true), TestContext.Current.CancellationToken);
        decision.Outcome.ShouldBe(CoworkPolicyOutcome.Ask);
    }

    [Fact]
    public async Task Skip_AllowsByPolicyUntilTainted()
    {
        var gate = new CoworkPolicyGate(new Store(), () => CoworkPermissionMode.Skip);
        var decision = await gate.DecideAsync(Request(CoworkApprovalRequirement.ByPolicy, CoworkToolEffect.Actuating, tainted: false), TestContext.Current.CancellationToken);
        decision.Outcome.ShouldBe(CoworkPolicyOutcome.Allow);
    }

    private static CoworkPolicyRequest Request(CoworkApprovalRequirement approval, CoworkToolEffect effect, bool tainted, bool grant = false) =>
        new(
            new CoworkSessionContext { SessionId = "s", WorkingDirectory = "/", TrustedFolders = [], ModelReadsImages = false },
            new CoworkToolDescriptor
            {
                Name = "artifact_read",
                ProviderId = "artifact",
                Description = "Read",
                InputSchema = JsonDocument.Parse("{}").RootElement,
                Effect = effect,
                Approval = approval,
            },
            new CoworkToolInvocation("s", "c", "artifact_read", JsonDocument.Parse("{}").RootElement, CoworkTurnOrigin.Local),
            new CoworkCallPreview("read", null, [], grant ? new CoworkGrantKey("artifact.write", "cowork") : null),
            tainted);

    private sealed class Store : ICoworkGrantStore
    {
        private readonly List<CoworkGrantKey> _keys = [];

        public ValueTask<bool> IsGrantedAsync(CoworkGrantKey key, CancellationToken cancellationToken) => ValueTask.FromResult(_keys.Contains(key));

        public ValueTask GrantAsync(CoworkGrantKey key, CancellationToken cancellationToken)
        {
            _keys.Add(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask RevokeAsync(CoworkGrantKey key, CancellationToken cancellationToken)
        {
            _ = _keys.Remove(key);
            return ValueTask.CompletedTask;
        }
    }
}
