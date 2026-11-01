using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Usage;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.Usage;

namespace Autonogrammer.Native.Composition.Activity;

/// <summary>
/// Binds the sub-agent, usage and notification sources through <see cref="ActivityTeePromptChannel"/>.
/// NotificationRequest and INotificationSink live in Harness.Notifications. Presentation never references them:
/// the bridge sits in Native and Native.Views. Scheduled-task notification kinds have no producer in this tree
/// until phase 75-03 events are present; those kinds are left without a producer.
/// </summary>
internal sealed class ActivityComposition
{
    /// <summary>Creates the composition.</summary>
    /// <param name="ledgerDirectory">The directory that holds <c>usage-ledger.jsonl</c>.</param>
    /// <param name="sessionId">The session id stamped on usage rows.</param>
    /// <param name="clock">The ledger clock, or null for the system clock.</param>
    public ActivityComposition(string ledgerDirectory, Func<string>? sessionId = null, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ledgerDirectory);
        Subagents = new AcpTaskToolActivitySource();
        Usage = new UsageLedgerFeeder(new FileUsageLedger(ledgerDirectory, clock), sessionId);
    }

    /// <summary>Gets the ACP sub-agent source.</summary>
    public AcpTaskToolActivitySource Subagents { get; }

    /// <summary>Gets the usage feeder. <see cref="MainView"/> reads it as <see cref="IUsageHistorySource"/>.</summary>
    public UsageLedgerFeeder Usage { get; }

    /// <summary>Gets the usage history <c>MainView.CreateExport</c> reads.</summary>
    public IUsageHistorySource UsageHistory => Usage;

    /// <summary>Wraps a prompt channel so the sources see the named events and the stream stays identical.</summary>
    /// <param name="inner">The slot channel.</param>
    /// <param name="also">An extra observer, for example the notification bridge. Null is none.</param>
    /// <returns>The tee.</returns>
    public ActivityTeePromptChannel Wrap(IPromptChannel inner, Action<HarnessEvent>? also = null)
    {
        return new ActivityTeePromptChannel(inner, evt =>
        {
            Subagents.Observe(evt);
            Usage.Observe(evt);
            also?.Invoke(evt);
        });
    }
}
