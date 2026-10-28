using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Native.Tests.AgentPanel.Views;

/// <summary>A session catalog the test edits; it stands in for the registry that owns the sessions.</summary>
internal sealed class FakeSessionCatalog : ISessionCatalog
{
    /// <summary>Gets the sessions the next snapshot returns.</summary>
    internal List<SessionSummary> Sessions { get; } = [];

    /// <summary>Gets or sets the exception <see cref="Snapshot"/> throws, or null.</summary>
    internal Exception? Failure { get; set; }

    /// <summary>Gets the number of snapshots taken.</summary>
    internal int SnapshotCount { get; private set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public IReadOnlyList<SessionSummary> Snapshot()
    {
        SnapshotCount++;
        if (Failure is not null)
        {
            throw Failure;
        }

        return [.. Sessions];
    }

    /// <summary>Raises <see cref="Changed"/>.</summary>
    internal void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Adds or replaces a session and raises <see cref="Changed"/>.</summary>
    /// <param name="summary">The session.</param>
    internal void Put(SessionSummary summary)
    {
        var index = Sessions.FindIndex(existing => string.Equals(existing.Id, summary.Id, StringComparison.Ordinal));
        if (index >= 0)
        {
            Sessions[index] = summary;
        }
        else
        {
            Sessions.Add(summary);
        }

        RaiseChanged();
    }

    /// <summary>Removes a session and raises <see cref="Changed"/>.</summary>
    /// <param name="id">The session.</param>
    internal void Remove(string id)
    {
        Sessions.RemoveAll(existing => string.Equals(existing.Id, id, StringComparison.Ordinal));
        RaiseChanged();
    }
}
