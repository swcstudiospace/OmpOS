using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Usage;
using Autonogrammer.Presentation.Export;
using Autonogrammer.Presentation.Usage;

namespace Autonogrammer.Native.Composition.Activity;

/// <summary>
/// Turns each <see cref="ContextUsageEvent"/> into a ledger row and exposes the ledger as
/// <see cref="IUsageHistorySource"/>. NotificationRequest and INotificationSink live in Harness.Notifications.
/// Presentation never references them: the bridge sits in Native and Native.Views.
/// </summary>
internal sealed class UsageLedgerFeeder : IUsageHistorySource
{
    private readonly FileUsageLedger _ledger;
    private readonly Func<string> _sessionId;

    /// <summary>Creates a feeder over a ledger.</summary>
    /// <param name="ledger">The ledger. It stores no prompt text.</param>
    /// <param name="sessionId">The session the next usage event belongs to.</param>
    public UsageLedgerFeeder(FileUsageLedger ledger, Func<string>? sessionId = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        _ledger = ledger;
        _sessionId = sessionId ?? (() => "session");
    }

    /// <summary>Gets the ledger.</summary>
    public FileUsageLedger Ledger => _ledger;

    /// <summary>Appends one usage event. Other events are ignored.</summary>
    /// <param name="evt">The harness event.</param>
    public void Observe(HarnessEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt is ContextUsageEvent usage)
        {
            _ledger.Append(_sessionId(), usage.Used, usage.Size, usage.Cost);
        }
    }

    /// <inheritdoc />
    public UsageHistory Read()
    {
        var records = _ledger.Read().Select(static entry => new UsageRecord(
            entry.SessionId,
            new DateTimeOffset(entry.Day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            UsageMeterModel.ExportModelName,
            entry.Used,
            0));
        return new UsageHistory(records);
    }
}
