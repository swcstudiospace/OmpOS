namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// The shape of the automation store, in memory and on disk: a version and the state of each automation by identifier. Both stores
/// apply their operations to a document, so they cannot drift apart.
/// </summary>
internal sealed class AutomationStoreDocument
{
    /// <summary>Gets or sets the document version. Only <see cref="FileAutomationStore.CurrentVersion"/> is read.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the state of each automation, by identifier.</summary>
    public Dictionary<string, StoredAutomationRecord>? Automations { get; set; }

    /// <summary>Creates an empty document.</summary>
    /// <returns>The document.</returns>
    public static AutomationStoreDocument Empty() =>
        new()
        {
            Version = FileAutomationStore.CurrentVersion,
            Automations = new Dictionary<string, StoredAutomationRecord>(StringComparer.Ordinal),
        };

    /// <summary>Checks that the document is usable.</summary>
    /// <param name="source">What the document was read from, for the message.</param>
    /// <exception cref="InvalidDataException">The version is unknown or an entry is empty.</exception>
    public void EnsureValid(string source)
    {
        if (Version != FileAutomationStore.CurrentVersion)
        {
            throw new InvalidDataException($"The automation store '{source}' has an unsupported version and was not read.");
        }

        if (Automations is null)
        {
            throw new InvalidDataException($"The automation store '{source}' has no automation table and was not read.");
        }

        foreach (var (id, record) in Automations)
        {
            if (record?.Automation is null || !string.Equals(record.Automation.Id, id, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The automation store '{source}' has an entry that does not match its identifier and was not read.");
            }

            record.Runs ??= [];
        }
    }

    /// <summary>Lists the definitions, in no promised order.</summary>
    /// <returns>The definitions.</returns>
    public IReadOnlyList<UserAutomation> List() => Automations!.Values.Select(static record => record.Automation!).ToArray();

    /// <summary>Reads one definition.</summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The definition or null.</returns>
    public UserAutomation? Get(string id) => Automations!.TryGetValue(id, out var record) ? record.Automation : null;

    /// <summary>Inserts or replaces a definition, keeping the history and cursor of a replaced one.</summary>
    /// <param name="automation">The definition.</param>
    public void Save(UserAutomation automation)
    {
        if (Automations!.TryGetValue(automation.Id, out var record))
        {
            record.Automation = automation;
        }
        else
        {
            Automations[automation.Id] = new StoredAutomationRecord { Automation = automation, Runs = [] };
        }
    }

    /// <summary>Removes an automation.</summary>
    /// <param name="id">The identifier.</param>
    /// <returns>True when it existed.</returns>
    public bool Remove(string id) => Automations!.Remove(id);

    /// <summary>Reads a schedule cursor.</summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The cursor or null.</returns>
    public DateTimeOffset? GetCursor(string id) => Automations!.TryGetValue(id, out var record) ? record.ScheduleCursor : null;

    /// <summary>Sets a schedule cursor.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cursor">The cursor.</param>
    /// <returns>True when the automation exists.</returns>
    public bool SetCursor(string id, DateTimeOffset cursor)
    {
        if (!Automations!.TryGetValue(id, out var record))
        {
            return false;
        }

        record.ScheduleCursor = cursor;
        return true;
    }

    /// <summary>Reads the history, newest first.</summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The entries.</returns>
    public IReadOnlyList<AutomationRunEntry> GetHistory(string id) =>
        Automations!.TryGetValue(id, out var record) ? record.Runs!.ToArray() : [];

    /// <summary>Inserts or replaces a run entry and trims the history.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>True when the automation exists.</returns>
    public bool UpsertRun(string id, AutomationRunEntry entry)
    {
        if (!Automations!.TryGetValue(id, out var record))
        {
            return false;
        }

        var runs = record.Runs!;
        var index = runs.FindIndex(existing => string.Equals(existing.RunId, entry.RunId, StringComparison.Ordinal));
        if (index >= 0)
        {
            runs[index] = entry;
        }
        else
        {
            runs.Insert(0, entry);
            if (runs.Count > AutomationLimits.MaxHistoryEntries)
            {
                runs.RemoveRange(AutomationLimits.MaxHistoryEntries, runs.Count - AutomationLimits.MaxHistoryEntries);
            }
        }

        return true;
    }
}
