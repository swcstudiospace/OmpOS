using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.UI.Tests;

/// <summary>A session catalog the test edits; it stands in for the registry that owns the sessions.</summary>
internal sealed class WorkspaceSessionCatalog : ISessionCatalog
{
    /// <summary>Gets the sessions the next snapshot returns.</summary>
    internal List<SessionSummary> Sessions { get; } = [];

    /// <summary>Gets or sets the exception the snapshot throws, or null.</summary>
    internal Exception? Failure { get; set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public IReadOnlyList<SessionSummary> Snapshot()
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        return [.. Sessions];
    }

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

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
