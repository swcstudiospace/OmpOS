using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;

namespace Autonogrammer.Cowork.Tools;

/// <summary>
/// A hand-written product fake. Tests script Describe, GetStateAsync, PreviewAsync and InvokeAsync.
/// ByProviderGate calls ask the broker with MustBeLocal when the probe says the call is a grant or a sensitive action.
/// </summary>
public sealed class FakeCoworkToolProvider : ICoworkToolProvider
{
    private readonly ICoworkApprovalBroker? _broker;
    private readonly CoworkProviderContractProbe? _probe;

    /// <summary>
    /// Creates the fake.
    /// </summary>
    /// <param name="broker">The broker a ByProviderGate provider must ask. Null refuses those asks as not approved.</param>
    /// <param name="probe">The contract probe, or null when a test scripts the fake directly.</param>
    public FakeCoworkToolProvider(ICoworkApprovalBroker? broker = null, CoworkProviderContractProbe? probe = null)
    {
        _broker = broker;
        _probe = probe;
    }

    /// <inheritdoc />
    public string ProviderId => "fake";

    /// <summary>Gets or sets the scripted descriptors. Null uses the contract pair.</summary>
    public Func<IReadOnlyList<CoworkToolDescriptor>>? DescribeScript { get; set; }

    /// <summary>Gets or sets the scripted state. Null is Available.</summary>
    public Func<CoworkSessionContext, CancellationToken, ValueTask<CoworkProviderState>>? StateScript { get; set; }

    /// <summary>Gets or sets the scripted preview.</summary>
    public Func<CoworkSessionContext, CoworkToolInvocation, CancellationToken, ValueTask<CoworkCallPreview>>? PreviewScript { get; set; }

    /// <summary>Gets or sets the scripted invoke.</summary>
    public Func<CoworkSessionContext, CoworkToolInvocation, CancellationToken, ValueTask<CoworkToolResult>>? InvokeScript { get; set; }

    /// <summary>Gets the sessions released by <see cref="EndSessionAsync"/>.</summary>
    public IReadOnlyCollection<string> ReleasedSessions => _released;

    private readonly HashSet<string> _released = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyList<CoworkToolDescriptor> Describe()
    {
        if (DescribeScript is not null)
        {
            return DescribeScript();
        }

        var schema = JsonDocument.Parse("""{"type":"object"}""").RootElement;
        return
        [
            new CoworkToolDescriptor
            {
                Name = "fake_sensitive",
                ProviderId = ProviderId,
                Description = "A sensitive fake action.",
                InputSchema = schema,
                Effect = CoworkToolEffect.Actuating,
                Approval = CoworkApprovalRequirement.ByProviderGate,
            },
            new CoworkToolDescriptor
            {
                Name = "fake_grant",
                ProviderId = ProviderId,
                Description = "A fake action that names a grant.",
                InputSchema = schema,
                Effect = CoworkToolEffect.Actuating,
                Approval = CoworkApprovalRequirement.ByProviderGate,
            },
        ];
    }

    /// <inheritdoc />
    public ValueTask<CoworkProviderState> GetStateAsync(CoworkSessionContext session, CancellationToken cancellationToken) =>
        StateScript?.Invoke(session, cancellationToken)
        ?? ValueTask.FromResult(new CoworkProviderState(CoworkProviderAvailability.Available, null));

    /// <inheritdoc />
    public ValueTask<CoworkCallPreview> PreviewAsync(
        CoworkSessionContext session,
        CoworkToolInvocation invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        if (PreviewScript is not null)
        {
            return PreviewScript(session, invocation, cancellationToken);
        }

        var grant = string.Equals(invocation.ToolName, "fake_grant", StringComparison.Ordinal) ? new CoworkGrantKey("fake.scope", "fake") : null;
        return ValueTask.FromResult(new CoworkCallPreview(invocation.ToolName, "fake-target", [], grant));
    }

    /// <inheritdoc />
    public async ValueTask<CoworkToolResult> InvokeAsync(
        CoworkSessionContext session,
        CoworkToolInvocation invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(invocation);
        if (InvokeScript is not null)
        {
            return await InvokeScript(session, invocation, cancellationToken).ConfigureAwait(false);
        }

        if (_broker is not null && (_probe is null || _probe.Action is CoworkContractAction.Grant or CoworkContractAction.Sensitive))
        {
            var grant = string.Equals(invocation.ToolName, "fake_grant", StringComparison.Ordinal) ? new CoworkGrantKey("fake.scope", "fake") : null;
            var decision = await _broker.RequestAsync(
                new CoworkApprovalRequest(session.SessionId, invocation.CallId, invocation.ToolName, new CoworkCallPreview(invocation.ToolName, "fake-target", [], grant), invocation.Origin, true),
                cancellationToken).ConfigureAwait(false);
            if (decision.Outcome is not CoworkApprovalOutcome.Allowed and not CoworkApprovalOutcome.AllowedAlways)
            {
                return CoworkToolResult.Failure(CoworkToolMessages.NotApproved);
            }
        }

        return CoworkToolResult.Text("ok");
    }

    /// <inheritdoc />
    public ValueTask EndSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        _ = _released.Add(sessionId);
        if (_probe is not null)
        {
            _probe.SessionReleased = true;
        }

        return ValueTask.CompletedTask;
    }
}
