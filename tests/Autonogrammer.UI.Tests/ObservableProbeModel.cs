using System.Collections.Specialized;
using System.ComponentModel;

namespace Autonogrammer.UI.Tests;

/// <summary>A model that raises both change events on demand and reports whether anyone is still subscribed.</summary>
internal sealed class ObservableProbeModel : INotifyPropertyChanged, INotifyCollectionChanged
{
    private string _value = "initial";

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>Gets a value indicating whether a handler is subscribed to <see cref="PropertyChanged"/>.</summary>
    public bool HasPropertyChangedSubscribers => PropertyChanged is not null;

    /// <summary>Gets a value indicating whether a handler is subscribed to <see cref="CollectionChanged"/>.</summary>
    public bool HasCollectionChangedSubscribers => CollectionChanged is not null;

    /// <summary>Gets the value the probe renders.</summary>
    public string Value => _value;

    /// <summary>Changes the value and raises <see cref="PropertyChanged"/>.</summary>
    /// <param name="value">The new value.</param>
    public void Change(string value)
    {
        _value = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }

    /// <summary>Raises <see cref="CollectionChanged"/> with a reset.</summary>
    public void ResetCollection()
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
