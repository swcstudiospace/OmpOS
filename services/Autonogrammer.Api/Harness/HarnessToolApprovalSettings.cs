using System.Collections.Frozen;
using Autonogrammer.Api.Options;
using Autonogrammer.SessionRuns;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Harness;

/// <summary>
/// The <see cref="IToolApprovalSettings"/> of the API, backed by <c>Harness:Acp:Approval</c>: <c>GrantableTools</c> is the ceiling every direct run
/// stores with its permission requests, and <c>DecisionTimeoutSeconds</c> is how long a run waits for the decision. The values are read once, when
/// the settings are created, from options that <see cref="EngineIsolationOptionsValidator"/> checks on start.
/// </summary>
internal sealed class HarnessToolApprovalSettings : IToolApprovalSettings
{
    /// <summary>Initializes a new instance of the <see cref="HarnessToolApprovalSettings"/> class.</summary>
    /// <param name="options">The harness options.</param>
    public HarnessToolApprovalSettings(IOptions<HarnessOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var approval = options.Value.Acp.Approval;
        DecisionTimeout = approval.DecisionTimeout;
        GrantableTools = approval.GrantableTools.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public TimeSpan DecisionTimeout { get; }

    /// <inheritdoc />
    public IReadOnlySet<string> GrantableTools { get; }
}
