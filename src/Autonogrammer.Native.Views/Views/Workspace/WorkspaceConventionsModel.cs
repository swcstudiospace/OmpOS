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

    /// <summary>Chooses the mode shown as active. <c>null</c> clears the choice.</summary>
    /// <param name="mode">The mode to choose, or <c>null</c>.</param>
    public void SelectMode(WorkspaceMode? mode) => _selectedModeId = mode?.Id;

    /// <summary>Stops watching the catalog. The catalog itself is owned by its creator and is not disposed here.</summary>
    public void Dispose() => _catalog.Changed -= OnCatalogChanged;

    private void OnCatalogChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
