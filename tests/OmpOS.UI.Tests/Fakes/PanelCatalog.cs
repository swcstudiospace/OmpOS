using OmpOS.Contracts.Agents;

namespace OmpOS.UI.Tests.Fakes;

/// <summary>The session catalog behind the rail: the sessions a test lists, and a way to make the snapshot fail.</summary>
internal sealed class PanelCatalog : ISessionCatalog
{
    /// <summary>Gets the sessions the catalog lists.</summary>
    internal List<SessionSummary> Sessions { get; } = [];

    /// <summary>Gets or sets the exception the next snapshot throws, or null.</summary>
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

    /// <summary>Adds or replaces a session and tells the model.</summary>
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
