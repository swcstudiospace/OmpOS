using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace Autonogrammer.UI.Components;

/// <summary>
/// Base class for a component that renders a framework-neutral presentation model. It subscribes to the model's
/// <see cref="INotifyPropertyChanged"/> and <see cref="INotifyCollectionChanged"/> events, re-renders on the component's own
/// dispatcher when one fires, and unsubscribes when the component is disposed.
/// </summary>
/// <remarks>
/// Call <see cref="Observe"/> from <c>OnInitialized</c> or <c>OnParametersSet</c> with the model (a parameter may change, so call it
/// again then: observing an already observed model is a no-op, and <see cref="StopObserving"/> releases one). A model event can
/// arrive on any thread, so the render is queued through <c>InvokeAsync</c>.
/// </remarks>
public abstract class ObservableComponent : ComponentBase, IDisposable
{
    private readonly List<object> _observed = [];
    private bool _disposed;

    /// <summary>Starts re-rendering this component whenever <paramref name="model"/> raises a change event.</summary>
    /// <param name="model">The model to watch. A model that implements neither change interface is ignored.</param>
    protected void Observe(object? model)
    {
        if (model is null || _disposed || IndexOfObserved(model) >= 0)
        {
            return;
        }

        var watched = false;
        if (model is INotifyPropertyChanged properties)
        {
            properties.PropertyChanged += OnPropertyChanged;
            watched = true;
        }

        if (model is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += OnCollectionChanged;
            watched = true;
        }

        if (watched)
        {
            _observed.Add(model);
        }
    }

    /// <summary>Stops re-rendering for <paramref name="model"/>; a model that was not observed is ignored.</summary>
    /// <param name="model">The model to release.</param>
    protected void StopObserving(object? model)
    {
        if (model is null)
        {
            return;
        }

        var index = IndexOfObserved(model);
        if (index < 0)
        {
            return;
        }

        _observed.RemoveAt(index);
        Unsubscribe(model);
    }

    /// <summary>Unsubscribes from every observed model.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (disposing)
        {
            foreach (var model in _observed)
            {
                Unsubscribe(model);
            }

            _observed.Clear();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private int IndexOfObserved(object model) => _observed.FindIndex(observed => ReferenceEquals(observed, model));

    private void Unsubscribe(object model)
    {
        if (model is INotifyPropertyChanged properties)
        {
            properties.PropertyChanged -= OnPropertyChanged;
        }

        if (model is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= OnCollectionChanged;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => QueueRender();

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueRender();

    private void QueueRender()
    {
        if (_disposed)
        {
            return;
        }

        _ = InvokeAsync(StateHasChanged);
    }
}
