using OmpOS.Harness.AgentLoop.Substrate;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Native.Agents;

/// <summary>
/// Connects the steering history of the panel to the Substrate ledger recorder. A <see cref="SteeringEntry"/> becomes a
/// <see cref="SteeringRecord"/> that carries the mode, the length and the 12-character SHA-256 prefix of the text: the text of the
/// message never leaves this adapter, because the entry's text is not read. The message itself was queued before the recorder runs,
/// so a record that fails only fails here, by exception, and the steering input marks that entry as not recorded.
/// </summary>
internal sealed class SteeringRecorderAdapter
{
    private readonly ISteeringRecorder _recorder;
    private readonly SessionScopeProvider _scope;

    /// <summary>Creates the adapter.</summary>
    /// <param name="recorder">Writes the record to the ledger.</param>
    /// <param name="scope">The live scope of the session the steering belongs to.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SteeringRecorderAdapter(ISteeringRecorder recorder, SessionScopeProvider scope)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(scope);
        _recorder = recorder;
        _scope = scope;
    }

    /// <summary>Maps an entry to the record that describes it. The entry's text is not read.</summary>
    /// <param name="entry">The accepted steering entry.</param>
    /// <returns>The record: mode, length, hash prefix and time.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public static SteeringRecord ToRecord(SteeringEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new SteeringRecord
        {
            Mode = entry.Mode == SteeringMode.Steer ? "steer" : "follow-up",
            Length = entry.Length,
            TextSha256Prefix = entry.TextSha256Prefix,
            At = entry.At,
        };
    }

    /// <summary>Records one entry in the ledger.</summary>
    /// <param name="entry">The accepted steering entry.</param>
    /// <param name="cancellationToken">Cancels the recording.</param>
    /// <returns>A task that completes when the ledger stored the record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The ledger did not store the record. The message is the recorder's plain-text reason.</exception>
    public async Task RecordAsync(SteeringEntry entry, CancellationToken cancellationToken)
    {
        var record = ToRecord(entry);
        var result = await _recorder.RecordAsync(_scope.Current, record, cancellationToken).ConfigureAwait(false);
        if (result.Outcome != SteeringRecordOutcome.Recorded)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Reason) ? "The steering action was not recorded." : result.Reason);
        }
    }
}
