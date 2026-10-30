using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Autonogrammer.Presentation.Observable;

/// <summary>
/// The base for presentation models that announce state changes through <see cref="INotifyPropertyChanged"/> (OQ-44).
/// The notification type is base .NET, so the models stay free of any UI framework; each head decides how to bind to it.
/// </summary>
public abstract class ObservableModel : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stores <paramref name="value"/> in <paramref name="field"/> and raises <see cref="PropertyChanged"/> only when the value changes.</summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">The changed property. Defaults to the calling member's name.</param>
    /// <returns>True when the value changed and the notification was raised.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        NotifyPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for one property.</summary>
    /// <param name="propertyName">The changed property. Defaults to the calling member's name.</param>
    protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
