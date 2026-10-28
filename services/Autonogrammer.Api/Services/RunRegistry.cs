using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Autonogrammer.Api.Services;

/// <summary>
/// Index of the hosted runs created through <c>POST /sessions/{id}/runs</c> in this process. Like <see cref="SessionRegistry"/>,
/// it lives for the life of the process: a run created before a restart is not found afterwards, and the run's output stays
/// readable from the session itself.
/// </summary>
internal sealed class RunRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, RunRecord> _runs = new(StringComparer.Ordinal);

    /// <summary>Creates and indexes the record of a new run.</summary>
    /// <param name="id">The run identifier.</param>
    /// <param name="sessionId">The identifier of the session the run executes in.</param>
    /// <param name="turn">The 1-based turn of the session the run executes.</param>
    /// <param name="createdAt">The time the run was accepted.</param>
    /// <returns>The indexed record.</returns>
    public RunRecord Create(string id, string sessionId, int turn, DateTimeOffset createdAt)
    {
        var record = new RunRecord(id, sessionId, turn, createdAt);
        _runs[id] = record;
        return record;
    }

    /// <summary>Finds the record of a run that belongs to the named session.</summary>
    /// <param name="sessionId">The session identifier named by the request.</param>
    /// <param name="runId">The run identifier named by the request.</param>
    /// <param name="record">The record when found.</param>
    /// <returns>True when the run exists and belongs to the session.</returns>
    public bool TryGet(string sessionId, string runId, [NotNullWhen(true)] out RunRecord? record)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(runId);
        if (_runs.TryGetValue(runId, out var found) && string.Equals(found.SessionId, sessionId, StringComparison.Ordinal))
        {
            record = found;
            return true;
        }

        record = null;
        return false;
    }

    /// <summary>Records that the harness started a run's prompt. A run this registry does not know, such as one started by a plain prompt, is ignored.</summary>
    /// <param name="runId">The run identifier.</param>
    public void MarkStarted(string runId)
    {
        ArgumentNullException.ThrowIfNull(runId);
        if (_runs.TryGetValue(runId, out var record))
        {
            record.MarkStarted();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var record in _runs.Values)
        {
            record.Dispose();
        }

        _runs.Clear();
    }
}
