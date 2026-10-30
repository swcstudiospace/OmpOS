using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;

namespace Autonogrammer.Cowork.Tools;

/// <summary>
/// Framework-free checks phases 71 to 73 run against a real provider.
/// ByProviderGate tools get Allow from the host gate and never Ask. The provider asks the broker with MustBeLocal
/// for grants and sensitive actions in every mode, including Skip and a tainted session.
/// </summary>
public abstract class CoworkProviderContractTests
{
    private readonly Func<ICoworkApprovalBroker, CoworkProviderContractProbe, ICoworkToolProvider> _create;

    /// <summary>
    /// Creates the base.
    /// </summary>
    /// <param name="create">Builds the provider under test with the broker and the probe.</param>
    protected CoworkProviderContractTests(Func<ICoworkApprovalBroker, CoworkProviderContractProbe, ICoworkToolProvider> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        _create = create;
    }

    /// <summary>Runs every D-24 and descriptor check. Throws one exception listing the failed rules.</summary>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task that completes when every rule passed.</returns>
#pragma warning disable MA0051 // One method runs every D-24 rule so a provider phase calls a single check.
    public async Task AssertContractAsync(CancellationToken cancellationToken = default)
    {
        var failed = new List<string>();
        var broker = new RecordingBroker();
        var probe = new CoworkProviderContractProbe();
        var provider = _create(broker, probe);
        var session = new CoworkSessionContext
        {
            SessionId = "contract",
            WorkingDirectory = "/",
            TrustedFolders = [],
            ModelReadsImages = true,
        };
        var state = await provider.GetStateAsync(session, cancellationToken).ConfigureAwait(false);
        if (state.Availability != CoworkProviderAvailability.Available)
        {
            failed.Add("provider-not-available");
        }

        var described = provider.Describe();
        if (described.Count == 0 || described.All(item => item.Approval != CoworkApprovalRequirement.ByProviderGate))
        {
            failed.Add("no-by-provider-gate-tool");
        }

        foreach (var descriptor in described)
        {
            if (!CoworkDescriptorValidator.TryValidate(descriptor, out _))
            {
                failed.Add("descriptor-invalid");
            }
        }

        var grants = new MemoryGrantStore();
        var gate = new CoworkPolicyGate(grants, () => CoworkPermissionMode.Skip);
        foreach (var descriptor in described.Where(item => item.Approval == CoworkApprovalRequirement.ByProviderGate))
        {
            var preview = new CoworkCallPreview(descriptor.Name, "target", [], null);
            var invocation = new CoworkToolInvocation(session.SessionId, "call", descriptor.Name, EmptyObject(), CoworkTurnOrigin.Local);
            foreach (var tainted in new[] { false, true })
            {
                var decision = await gate.DecideAsync(
                    new CoworkPolicyRequest(session, descriptor, invocation, preview, tainted),
                    cancellationToken).ConfigureAwait(false);
                if (decision.Outcome != CoworkPolicyOutcome.Allow)
                {
                    failed.Add("host-gate-asked");
                }
            }
        }

        foreach (var mode in new[] { CoworkPermissionMode.Manual, CoworkPermissionMode.Auto, CoworkPermissionMode.Skip })
        {
            foreach (var tainted in new[] { false, true })
            {
                foreach (var action in new[] { CoworkContractAction.Grant, CoworkContractAction.Sensitive })
                {
                    broker.Calls.Clear();
                    probe.Mode = mode;
                    probe.Tainted = tainted;
                    probe.Action = action;
                    var tool = action == CoworkContractAction.Grant ? GrantTool(described) : SensitiveTool(described);
                    if (tool is null)
                    {
                        failed.Add(action == CoworkContractAction.Grant ? "no-grant-tool" : "no-sensitive-tool");
                        continue;
                    }

                    var invocation = new CoworkToolInvocation(session.SessionId, "call", tool, EmptyObject(), CoworkTurnOrigin.Local);
                    _ = await provider.InvokeAsync(session, invocation, cancellationToken).ConfigureAwait(false);
                    if (!broker.Calls.Exists(call => call.MustBeLocal))
                    {
                        failed.Add("broker-not-asked-must-be-local");
                    }
                }
            }
        }

        probe.SessionReleased = false;
        await provider.EndSessionAsync(session.SessionId, cancellationToken).ConfigureAwait(false);
        if (!probe.SessionReleased)
        {
            failed.Add("end-session-did-not-release");
        }

        if (failed.Count > 0)
        {
            throw new CoworkProviderContractViolationException(failed.Distinct(StringComparer.Ordinal).ToArray());
        }
    }
#pragma warning restore MA0051

    private static string? GrantTool(IReadOnlyList<CoworkToolDescriptor> described) =>
        described.FirstOrDefault(item => item.Name.Contains("grant", StringComparison.Ordinal))?.Name
        ?? described.FirstOrDefault(item => item.Approval == CoworkApprovalRequirement.ByProviderGate)?.Name;

    private static string? SensitiveTool(IReadOnlyList<CoworkToolDescriptor> described) =>
        described.FirstOrDefault(item => item.Name.Contains("sensitive", StringComparison.Ordinal))?.Name
        ?? described.FirstOrDefault(item => item.Approval == CoworkApprovalRequirement.ByProviderGate && item.Effect == CoworkToolEffect.Actuating)?.Name;

    private static JsonElement EmptyObject() => JsonDocument.Parse("{}").RootElement;

    private sealed class RecordingBroker : ICoworkApprovalBroker
    {
        public List<CoworkApprovalRequest> Calls { get; } = [];

        public ValueTask<CoworkApprovalDecision> RequestAsync(CoworkApprovalRequest request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return ValueTask.FromResult(new CoworkApprovalDecision(CoworkApprovalOutcome.Allowed));
        }
    }

    private sealed class MemoryGrantStore : ICoworkGrantStore
    {
        public ValueTask<bool> IsGrantedAsync(CoworkGrantKey key, CancellationToken cancellationToken) => ValueTask.FromResult(false);

        public ValueTask GrantAsync(CoworkGrantKey key, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask RevokeAsync(CoworkGrantKey key, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
