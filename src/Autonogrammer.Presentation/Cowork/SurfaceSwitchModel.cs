using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// The one switch both heads bind. A change of <see cref="Selected"/> hides one surface and shows the other.
/// It does not stop, pause or dispose a session. Persistence strings are <c>code</c> and <c>cowork</c>.
/// </summary>
public sealed class SurfaceSwitchModel : ObservableModel
{
    /// <summary>The persisted value for Code.</summary>
    public const string CodeValue = "code";

    /// <summary>The persisted value for Cowork.</summary>
    public const string CoworkValue = "cowork";

    private SessionSurface _selected = SessionSurface.Code;
    private bool _coworkAvailable;

    /// <summary>Occurs after the selection or the Cowork availability changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the surface the head shows. The other surface stays alive.</summary>
    public SessionSurface Selected => _selected;

    /// <summary>Gets whether Cowork may be selected. Native reads the account gate; Web reads access.</summary>
    public bool IsCoworkAvailable => _coworkAvailable;

    /// <summary>Gets whether the switch is shown. It is hidden while Cowork is unavailable, and Code stays selected.</summary>
    public bool IsSwitchVisible => _coworkAvailable;

    /// <summary>Gets the persisted form of <see cref="Selected"/>.</summary>
    public string Persisted => Format(_selected);

    /// <summary>Writes the persisted string for a surface.</summary>
    /// <param name="surface">The surface.</param>
    /// <returns><c>code</c> or <c>cowork</c>.</returns>
    public static string Format(SessionSurface surface) => surface switch
    {
        SessionSurface.Code => CodeValue,
        SessionSurface.Cowork => CoworkValue,
        _ => throw new ArgumentOutOfRangeException(nameof(surface)),
    };

    /// <summary>Reads a persisted string. An unusable value returns null and does not throw.</summary>
    /// <param name="saved">The stored text, or null.</param>
    /// <returns>The surface, or null when the text is not <c>code</c> or <c>cowork</c>.</returns>
    public static SessionSurface? Parse(string? saved)
    {
        if (string.Equals(saved, CodeValue, StringComparison.Ordinal))
        {
            return SessionSurface.Code;
        }

        if (string.Equals(saved, CoworkValue, StringComparison.Ordinal))
        {
            return SessionSurface.Cowork;
        }

        return null;
    }

    /// <summary>
    /// Restores a stored surface only when Cowork is available. Otherwise the selection stays Code and nothing is thrown.
    /// An unusable stored value also stays Code.
    /// </summary>
    /// <param name="saved">The stored text from <see cref="Format"/>.</param>
    /// <param name="available">Whether Cowork may be selected.</param>
    public void Restore(string? saved, bool available)
    {
        _coworkAvailable = available;
        var parsed = Parse(saved);
        _selected = available && parsed == SessionSurface.Cowork ? SessionSurface.Cowork : SessionSurface.Code;
        Notify();
    }

    /// <summary>Records whether Cowork may be selected. Becoming unavailable returns the selection to Code without an error.</summary>
    /// <param name="available">Whether Cowork may be selected.</param>
    public void SetCoworkAvailable(bool available)
    {
        if (_coworkAvailable == available && (available || _selected == SessionSurface.Code))
        {
            return;
        }

        _coworkAvailable = available;
        if (!available)
        {
            _selected = SessionSurface.Code;
        }

        Notify();
    }

    /// <summary>Selects a surface. Cowork is ignored while it is unavailable, and the selection stays Code.</summary>
    /// <param name="surface">The surface to show.</param>
    public void Select(SessionSurface surface)
    {
        if (surface == SessionSurface.Cowork && !_coworkAvailable)
        {
            return;
        }

        if (surface is not SessionSurface.Code and not SessionSurface.Cowork)
        {
            throw new ArgumentOutOfRangeException(nameof(surface));
        }

        if (_selected == surface)
        {
            return;
        }

        _selected = surface;
        Notify();
    }

    private void Notify()
    {
        NotifyPropertyChanged(nameof(Selected));
        NotifyPropertyChanged(nameof(IsCoworkAvailable));
        NotifyPropertyChanged(nameof(IsSwitchVisible));
        NotifyPropertyChanged(nameof(Persisted));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
