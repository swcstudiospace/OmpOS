namespace Autonogrammer.Accounts.Tests.Support;

/// <summary>An ordered record of events from several test doubles, so a test can assert what happened before what.</summary>
internal sealed class Journal
{
    private readonly Lock _gate = new();
    private readonly List<string> _entries = [];

    /// <summary>Gets a snapshot of the entries in order.</summary>
    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>Adds an entry.</summary>
    /// <param name="entry">The entry.</param>
    public void Add(string entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }
}
