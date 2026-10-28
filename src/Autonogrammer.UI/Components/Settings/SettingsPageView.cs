using Microsoft.AspNetCore.Components;

namespace Autonogrammer.UI.Components.Settings;

/// <summary>
/// Base class of a Settings page component: it carries the page model as a parameter and re-renders the component whenever the model
/// changes. A page component is keyed by its model in the dialog, so a new model always means a new component.
/// </summary>
/// <typeparam name="TPage">The page model type the component renders.</typeparam>
public abstract class SettingsPageView<TPage> : ObservableComponent
    where TPage : class
{
    private TPage? _watched;

    /// <summary>Gets or sets the page model the component renders.</summary>
    [Parameter, EditorRequired]
    public TPage Page { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_watched, Page))
        {
            StopObserving(_watched);
            _watched = Page;
        }

        Observe(Page);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopObserving(_watched);
        }

        base.Dispose(disposing);
    }
}
