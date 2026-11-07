using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// The Cowork layout: the rail, the conversation region and the dock, plus the dock's open state and registered panels.
/// One registered panel shows no tab strip. There is no board tab.
/// </summary>
public sealed class CoworkShellModel : ObservableModel, IDisposable
{
    private readonly List<IDockPanel> _panels = [];
    private bool _dockOpen = true;
    private string? _selectedPanelId;

    /// <summary>Creates the shell and its rail.</summary>
    /// <param name="catalog">The Cowork catalog the rail reads.</param>
    public CoworkShellModel(ICoworkSessionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Rail = new CoworkSessionListModel(catalog);
    }

    /// <summary>Occurs after the dock opens or closes, or the panel registry changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the three regions, in cycle order. There is no board region.</summary>
    public IReadOnlyList<CoworkShellPart> Parts { get; } = [CoworkShellPart.Rail, CoworkShellPart.Conversation, CoworkShellPart.Dock];

    /// <summary>Gets the rail.</summary>
    public CoworkSessionListModel Rail { get; }

    /// <summary>Gets whether the dock region is open.</summary>
    public bool IsDockOpen => _dockOpen;

    /// <summary>Gets the registered panels, in registration order.</summary>
    public IReadOnlyList<IDockPanel> Panels => _panels;

    /// <summary>Gets the selected panel identifier, or null when no panel is registered.</summary>
    public string? SelectedPanelId => _selectedPanelId;

    /// <summary>Gets the selected panel, or null.</summary>
    public IDockPanel? SelectedPanel => _selectedPanelId is null
        ? null
        : _panels.Find(panel => string.Equals(panel.Id, _selectedPanelId, StringComparison.Ordinal));

    /// <summary>Gets whether the dock shows a tab strip. One panel shows no strip.</summary>
    public bool ShowTabStrip => _panels.Count > 1;

    /// <summary>Opens the dock.</summary>
    public void OpenDock() => SetDockOpen(true);

    /// <summary>Closes the dock. Registered panels stay registered.</summary>
    public void CloseDock() => SetDockOpen(false);

    /// <summary>Registers a panel. The same id replaces the panel in place and keeps its order.</summary>
    /// <param name="panel">The panel. Its content is a presentation view model.</param>
    public void RegisterPanel(IDockPanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (string.IsNullOrWhiteSpace(panel.Id))
        {
            throw new ArgumentException("The panel id is empty.", nameof(panel));
        }
        var index = _panels.FindIndex(existing => string.Equals(existing.Id, panel.Id, StringComparison.Ordinal));
        if (index >= 0)
        {
            _panels[index] = panel;
        }
        else
        {
            _panels.Add(panel);
        }

        _selectedPanelId ??= panel.Id;
        NotifyPanels();
    }

    /// <summary>Removes a panel. The selected panel, if removed, becomes the first remaining panel.</summary>
    /// <param name="id">The panel identifier.</param>
    public void UnregisterPanel(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var removed = _panels.RemoveAll(panel => string.Equals(panel.Id, id, StringComparison.Ordinal));
        if (removed == 0)
        {
            return;
        }

        if (string.Equals(_selectedPanelId, id, StringComparison.Ordinal))
        {
            _selectedPanelId = _panels.Count == 0 ? null : _panels[0].Id;
        }

        NotifyPanels();
    }

    /// <summary>Selects a registered panel. An unknown id is ignored.</summary>
    /// <param name="id">The panel identifier.</param>
    public void SelectPanel(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (_panels.TrueForAll(panel => !string.Equals(panel.Id, id, StringComparison.Ordinal)))
        {
            return;
        }

        if (string.Equals(_selectedPanelId, id, StringComparison.Ordinal))
        {
            return;
        }

        _selectedPanelId = id;
        NotifyPropertyChanged(nameof(SelectedPanelId));
        NotifyPropertyChanged(nameof(SelectedPanel));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public void Dispose() => Rail.Dispose();

    private void SetDockOpen(bool open)
    {
        if (_dockOpen == open)
        {
            return;
        }

        _dockOpen = open;
        NotifyPropertyChanged(nameof(IsDockOpen));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyPanels()
    {
        NotifyPropertyChanged(nameof(Panels));
        NotifyPropertyChanged(nameof(SelectedPanelId));
        NotifyPropertyChanged(nameof(SelectedPanel));
        NotifyPropertyChanged(nameof(ShowTabStrip));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
