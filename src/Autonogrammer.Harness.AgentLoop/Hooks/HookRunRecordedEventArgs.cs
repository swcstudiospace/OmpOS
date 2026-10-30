using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>Event data of <see cref="IHooksService.RunRecorded"/>: one hook was considered in one dispatch.</summary>
public sealed class HookRunRecordedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="HookRunRecordedEventArgs"/> class.</summary>
    /// <param name="record">The record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public HookRunRecordedEventArgs(HookRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Record = record;
    }

    /// <summary>Gets the record. Its output excerpts are not redacted; redact them before they leave the process (see <see cref="HookRunRecordProjector"/>).</summary>
    public HookRunRecord Record { get; }
}
