using System.ComponentModel;

namespace OmpOS.Native.Views.AgentPanel;

/// <summary>
/// Holds the one subscription a view keeps to the property changes of its model. A view sets the model when it is attached and clears
/// it when it is detached, so a detached view or a swapped data context never stays subscribed.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
internal sealed class ModelWatcher<TModel>
    where TModel : class, INotifyPropertyChanged
{
    private readonly Action _changed;
    private TModel? _model;

    /// <summary>Creates a watcher that calls <paramref name="changed"/> whenever the watched model raises a property change.</summary>
    /// <param name="changed">The redraw action.</param>
    /// <exception cref="ArgumentNullException"><paramref name="changed"/> is null.</exception>
    public ModelWatcher(Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);

        _changed = changed;
    }

    /// <summary>Gets the model being watched, or null.</summary>
    public TModel? Model => _model;

    /// <summary>Watches <paramref name="model"/> instead of the previous model.</summary>
    /// <param name="model">The new model, or null to stop watching.</param>
    public void Set(TModel? model)
    {
        if (ReferenceEquals(_model, model))
        {
            return;
        }

        if (_model is not null)
        {
            _model.PropertyChanged -= OnChanged;
        }

        _model = model;
        if (_model is not null)
        {
            _model.PropertyChanged += OnChanged;
        }
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => _changed();
}
