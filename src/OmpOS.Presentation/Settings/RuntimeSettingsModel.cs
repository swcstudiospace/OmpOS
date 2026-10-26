using System.Globalization;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// The Runtime section of the settings window: idle stop and completed-session retention. Edits are drafts. An entry outside
/// <see cref="RuntimeSettingsLimits"/> (or not a whole number) is rejected inline through <see cref="IdleStopError"/> or
/// <see cref="RetentionError"/> and can never be saved; <see cref="Reset"/> returns the draft to the section defaults and
/// <see cref="Discard"/> returns it to the last saved values. The saved values change only through <see cref="TrySave"/>.
/// </summary>
public sealed class RuntimeSettingsModel : ObservableModel
{
    private RuntimeSettingsValues _saved;
    private string _idleStopText;
    private string? _idleStopError;
    private string _retentionText;
    private string? _retentionError;

    /// <summary>Initializes a new instance of the <see cref="RuntimeSettingsModel"/> class.</summary>
    /// <param name="saved">The previously saved values, or null for the section defaults.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="saved"/> is outside <see cref="RuntimeSettingsLimits"/>.</exception>
    public RuntimeSettingsModel(RuntimeSettingsValues? saved = null)
    {
        _saved = saved ?? RuntimeSettingsValues.Defaults;
        if (!RuntimeSettingsLimits.AreInRange(_saved))
        {
            throw new ArgumentOutOfRangeException(nameof(saved), _saved, "The saved runtime settings are outside the allowed range.");
        }

        _idleStopText = Format(_saved.IdleStopHours);
        _retentionText = Format(_saved.RetentionDays);
    }

    /// <summary>Gets the last saved values.</summary>
    public RuntimeSettingsValues Saved => _saved;

    /// <summary>Gets the idle stop entry as typed, trimmed.</summary>
    public string IdleStopText => _idleStopText;

    /// <summary>Gets the inline error for the idle stop entry, or null when it is valid.</summary>
    public string? IdleStopError => _idleStopError;

    /// <summary>Gets the retention entry as typed, trimmed.</summary>
    public string RetentionText => _retentionText;

    /// <summary>Gets the inline error for the retention entry, or null when it is valid.</summary>
    public string? RetentionError => _retentionError;

    /// <summary>Gets a value indicating whether either entry is currently rejected.</summary>
    public bool HasErrors => _idleStopError is not null || _retentionError is not null;

    /// <summary>Gets a value indicating whether the draft differs from the saved values. A rejected entry counts as a change.</summary>
    public bool IsDirty => Draft is not { } draft || draft != _saved;

    /// <summary>Gets the draft values, or null while either entry is rejected.</summary>
    public RuntimeSettingsValues? Draft =>
        RuntimeSettingsLimits.ValidateIdleStop(_idleStopText, out int hours) is null
        && RuntimeSettingsLimits.ValidateRetention(_retentionText, out int days) is null
            ? new RuntimeSettingsValues(hours, days)
            : null;

    /// <summary>Sets the idle stop entry and validates it. A rejected entry keeps its text so it can be corrected.</summary>
    /// <param name="text">The entered text in whole hours.</param>
    /// <returns>True when the entry is valid.</returns>
    public bool SetIdleStop(string? text)
    {
        var entry = text?.Trim() ?? string.Empty;
        var error = RuntimeSettingsLimits.ValidateIdleStop(entry, out _);
        SetProperty(ref _idleStopText, entry, nameof(IdleStopText));
        SetProperty(ref _idleStopError, error, nameof(IdleStopError));
        NotifySummary();
        return error is null;
    }

    /// <summary>Sets the retention entry and validates it. A rejected entry keeps its text so it can be corrected.</summary>
    /// <param name="text">The entered text in whole days.</param>
    /// <returns>True when the entry is valid.</returns>
    public bool SetRetention(string? text)
    {
        var entry = text?.Trim() ?? string.Empty;
        var error = RuntimeSettingsLimits.ValidateRetention(entry, out _);
        SetProperty(ref _retentionText, entry, nameof(RetentionText));
        SetProperty(ref _retentionError, error, nameof(RetentionError));
        NotifySummary();
        return error is null;
    }

    /// <summary>Returns the draft to the section defaults and clears any inline error. The saved values are unchanged until <see cref="TrySave"/>.</summary>
    public void Reset() => Load(RuntimeSettingsValues.Defaults);

    /// <summary>Returns the draft to the last saved values and clears any inline error.</summary>
    public void Discard() => Load(_saved);

    /// <summary>Saves the draft when both entries are valid.</summary>
    /// <returns>The values now saved, or null when an entry is rejected and nothing was saved.</returns>
    public RuntimeSettingsValues? TrySave()
    {
        if (Draft is not { } draft)
        {
            return null;
        }

        _saved = draft;
        Load(draft);
        NotifyPropertyChanged(nameof(Saved));
        return draft;
    }

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    private void Load(RuntimeSettingsValues values)
    {
        SetProperty(ref _idleStopText, Format(values.IdleStopHours), nameof(IdleStopText));
        SetProperty(ref _idleStopError, null, nameof(IdleStopError));
        SetProperty(ref _retentionText, Format(values.RetentionDays), nameof(RetentionText));
        SetProperty(ref _retentionError, null, nameof(RetentionError));
        NotifySummary();
    }

    private void NotifySummary()
    {
        NotifyPropertyChanged(nameof(HasErrors));
        NotifyPropertyChanged(nameof(IsDirty));
        NotifyPropertyChanged(nameof(Draft));
    }
}
