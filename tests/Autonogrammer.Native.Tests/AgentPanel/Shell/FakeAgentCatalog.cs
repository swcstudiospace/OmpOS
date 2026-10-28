using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>The session registry of a shell under test: a list the test edits, with the changed notification the real registry raises.</summary>
internal sealed class FakeAgentCatalog : ISessionCatalog
{
    private readonly List<SessionSummary> _sessions = [];

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Gets or sets the failure the next snapshots throw, or null when they succeed.</summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<SessionSummary> Snapshot()
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        return [.. _sessions];
    }

    /// <summary>Adds a session, or replaces the one with the same identifier, and raises the changed notification.</summary>
    /// <param name="summary">The session.</param>
    public void Put(SessionSummary summary)
    {
        var index = _sessions.FindIndex(existing => string.Equals(existing.Id, summary.Id, StringComparison.Ordinal));
        if (index >= 0)
        {
            _sessions[index] = summary;
        }
        else
        {
            _sessions.Add(summary);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Gets the session with the identifier, or null.</summary>
    /// <param name="id">The session identifier.</param>
    /// <returns>The session, or null.</returns>
    public SessionSummary? Find(string id) => _sessions.Find(existing => string.Equals(existing.Id, id, StringComparison.Ordinal));
}
