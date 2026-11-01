using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// The ordered header-end entries both surfaces draw. Placement belongs to the heads. An entry added once appears on both.
/// </summary>
public sealed class SurfaceChromeModel : ObservableModel
{
    private readonly List<SurfaceChromeEntry> _entries = [];

    /// <summary>Occurs after an entry is added.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the entries in the order they were added.</summary>
    public IReadOnlyList<SurfaceChromeEntry> Entries => _entries;

    /// <summary>Adds an entry. A second add of the same <see cref="SurfaceChromeEntry.Id"/> is ignored.</summary>
    /// <param name="entry">The entry. The host constructs it.</param>
    public void Add(SurfaceChromeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_entries.Exists(existing => string.Equals(existing.Id, entry.Id, StringComparison.Ordinal)))
        {
            return;
        }

        _entries.Add(entry);
        NotifyPropertyChanged(nameof(Entries));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
