using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>
/// The permission resolver that applies the run mode. It sits between the ACP session and the resolver that asks the user. A call the policy
/// denies is denied; a call it allows is allowed; a call it asks about is passed to the resolver that asks the user. It can only restrict or ask: the
/// ACP session has already admitted the call through its approval policy before any resolver is consulted, and the optional <c>ceiling</c> repeats that check here, so a
/// run mode never grants a call the existing approval policy forbids. The one exception is <see cref="RunMode.RunEverything"/>, where the user has chosen to allow
/// everything and the ceiling is not consulted (the head then builds an approval policy that admits every kind).
/// </summary>
/// <remarks>Order: hooks first (a hook may deny before omp asks anyone), then the approval policy ceiling, then this resolver's deny rules, mode and allow rules, then the user.</remarks>
public sealed class RunModePermissionResolver : IPermissionResolver
{
    private const int MaxSubjectLength = 200;

    private readonly IPermissionResolver _askUser;
    private readonly Func<RunModePolicy> _policy;
    private readonly Func<PermissionRequestEvent, bool>? _ceiling;
    private readonly IRunModeAuditSink? _audit;
    private readonly TimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="RunModePermissionResolver"/> class.</summary>
    /// <param name="askUser">The resolver that asks the user. It receives only the calls the policy cannot settle.</param>
    /// <param name="policy">Returns the policy in force. It is called for every request, so a head can swap the policy when a file or the mode changes.</param>
    /// <param name="ceiling">The existing approval policy as a predicate: true when it admits the request. Null means no extra check.</param>
    /// <param name="audit">Receives every decision, or null.</param>
    /// <param name="clock">The clock for the audit record, or null for the system clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="askUser"/> or <paramref name="policy"/> is null.</exception>
    public RunModePermissionResolver(
        IPermissionResolver askUser,
        Func<RunModePolicy> policy,
        Func<PermissionRequestEvent, bool>? ceiling = null,
        IRunModeAuditSink? audit = null,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(askUser);
        ArgumentNullException.ThrowIfNull(policy);
        _askUser = askUser;
        _policy = policy;
        _ceiling = ceiling;
        _audit = audit;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = _policy();
        var parsed = RunModeRequestParser.Parse(request);
        if (policy.Mode != RunMode.RunEverything && _ceiling is not null && !_ceiling(request))
        {
            Record(request, parsed, new RunModeDecision(RunModeOutcome.Deny, RunModeStage.PolicyCeiling, "The approval policy does not admit this kind of call."));
            return PermissionDecision.Deny;
        }

        var decision = policy.Decide(parsed);
        Record(request, parsed, decision);
        switch (decision.Outcome)
        {
            case RunModeOutcome.Deny:
                return PermissionDecision.Deny;
            case RunModeOutcome.Allow:
                return PermissionDecision.Allow;
            default:
                return await _askUser.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string Subject(RunModeRequest parsed)
    {
        var text = parsed.Kind switch
        {
            RunModeToolKind.Shell => parsed.Command,
            RunModeToolKind.Read or RunModeToolKind.Write => parsed.Paths is { Count: > 0 } ? string.Join(", ", parsed.Paths) : null,
            RunModeToolKind.Fetch => parsed.FetchAddress,
            RunModeToolKind.Mcp => parsed.McpServer + ":" + parsed.McpTool,
            _ => null,
        } ?? parsed.ToolName;
        return text.Length <= MaxSubjectLength ? text : text[..MaxSubjectLength];
    }

    private void Record(PermissionRequestEvent request, RunModeRequest parsed, RunModeDecision decision) =>
        _audit?.Record(new RunModeAuditEntry(request.CallId, parsed.ToolName, parsed.Kind, Subject(parsed), decision, _clock.GetUtcNow()));
}
