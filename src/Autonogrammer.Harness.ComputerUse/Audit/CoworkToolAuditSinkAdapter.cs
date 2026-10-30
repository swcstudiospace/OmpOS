using Autonogrammer.Cowork.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Audit;

/// <summary>
/// <see cref="ICoworkToolAudit"/> sink. The entry has no arguments and no result bytes.
/// This type has no ledger and writes nothing to cowork_events.
/// Plan note: re-read the real ICoworkToolAudit signature once phase 70 field names settle and reconcile names without changing redaction.
/// </summary>
public sealed class CoworkToolAuditSinkAdapter : ICoworkToolAudit
{
    private readonly ComputerAuditLog _log;

    /// <summary>Creates the adapter.</summary>
    /// <param name="log">The local log.</param>
    public CoworkToolAuditSinkAdapter(ComputerAuditLog log) => _log = log;

    /// <inheritdoc/>
    public ValueTask RecordAsync(CoworkToolAuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _ = cancellationToken;
        _ = _log.TryAppend(new ComputerAuditWrite
        {
            SessionId = entry.SessionId,
            Tool = entry.ToolName,
            Summary = entry.ProviderId,
            AppIdentity = entry.Target ?? string.Empty,
            Title = string.Empty,
            Decision = entry.Policy.ToString(),
            ApprovalOrigin = entry.Origin.ToString(),
            Outcome = entry.IsError ? "error" : "ok",
        });
        return ValueTask.CompletedTask;
    }
}
