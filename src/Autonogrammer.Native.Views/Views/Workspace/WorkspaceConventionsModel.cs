using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Workspace;

namespace Autonogrammer.Native.Views.Workspace;

/// <summary>
/// The rules, skills, modes and diagnostics of one workspace, read from its <see cref="WorkspaceCatalog"/>, and the mode
/// chosen in the mode picker. Every read takes the catalog's current snapshot, so a reload is visible at the next read.
/// The chosen mode is only shown here; no run path reads it.
/// </summary>
internal sealed class WorkspaceConventionsModel : IDisposable
{
    private readonly WorkspaceCatalog _catalog;
    private readonly List<LegacyNotice> _shown = [];
    private string? _selectedModeId;

    /// <summary>Creates the model over a catalog and watches the catalog for reloads until it is disposed.</summary>
    /// <param name="catalog">The workspace catalog to read.</param>
    public WorkspaceConventionsModel(WorkspaceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        _catalog.Changed += OnCatalogChanged;
    }

    /// <summary>
    /// Raised after the catalog reloads, on the thread that reloaded it. Views marshal to their own thread before they draw.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>Gets the catalog's current snapshot.</summary>
    public WorkspaceSnapshot Snapshot => _catalog.Current;

    /// <summary>Gets the mode chosen in the picker, or <c>null</c> when none is chosen or the chosen mode is no longer loaded.</summary>
    public WorkspaceMode? SelectedMode => Snapshot.Modes.FirstOrDefault(mode => string.Equals(mode.Id, _selectedModeId, StringComparison.Ordinal));

    /// <summary>Gets the legacy notices seen so far. A second read of the same tree adds nothing.</summary>
    public IReadOnlyList<LegacyNotice> Notices
    {
        get
        {
            foreach (var notice in _catalog.LegacyNotices)
            {
                if (_shown.All(shown => !string.Equals(shown.LegacyPath, notice.LegacyPath, StringComparison.Ordinal)))
                {
                    _shown.Add(notice);
                }
            }

            return _shown;
        }
    }

    /// <summary>Gets the documented repository move command. The view copies it and never executes it.</summary>
    public string RepoMoveCommand
    {
        get
        {
            _ = _catalog;
            return LegacyConventionDiscovery.MoveCommand("project");
        }
    }

    /// <summary>Chooses the mode shown as active. <c>null</c> clears the choice.</summary>
    /// <param name="mode">The mode to choose, or <c>null</c>.</param>
    public void SelectMode(WorkspaceMode? mode) => _selectedModeId = mode?.Id;

    /// <summary>Moves the user-scope legacy directory onto the current directory when the current directory is absent.</summary>
    /// <param name="homeDirectory">The user home.</param>
    /// <returns>Moved, already migrated, or a conflict naming both paths.</returns>
    public LegacyMoveResult MoveUserLegacyDirectory(string homeDirectory)
    {
        _ = _catalog;
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        var ordered = LegacyConventionDiscovery.OrderedCandidates(homeDirectory);
        var newer = ordered[0];
        var legacy = ordered[1];
        if (!Directory.Exists(legacy))
        {
            return new LegacyMoveResult(LegacyMoveStatus.AlreadyMigrated, newer, legacy, null);
        }

        if (Directory.Exists(newer))
        {
            return new LegacyMoveResult(LegacyMoveStatus.Conflict, newer, legacy, "Conflict: both directories exist. Nothing was merged. " + legacy + " and " + newer);
        }

        Directory.Move(legacy, newer);
        return new LegacyMoveResult(LegacyMoveStatus.Moved, newer, legacy, null);
    }

    /// <summary>Stops watching the catalog. The catalog itself is owned by its creator and is not disposed here.</summary>
    public void Dispose() => _catalog.Changed -= OnCatalogChanged;


    private void OnCatalogChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}

#pragma warning disable MA0048
/// <summary>The outcome of a user-scope directory move.</summary>
internal enum LegacyMoveStatus
{
    /// <summary>The legacy directory was moved.</summary>
    Moved,

    /// <summary>The legacy directory was already absent.</summary>
    AlreadyMigrated,

    /// <summary>Both directories exist. Nothing was moved or merged.</summary>
    Conflict,
}

/// <summary>The result of a user-scope directory move.</summary>
/// <param name="Status">What happened.</param>
/// <param name="NewPath">The current directory.</param>
/// <param name="LegacyPath">The legacy directory.</param>
/// <param name="Message">The conflict sentence, or null.</param>
internal sealed record LegacyMoveResult(LegacyMoveStatus Status, string NewPath, string LegacyPath, string? Message);
