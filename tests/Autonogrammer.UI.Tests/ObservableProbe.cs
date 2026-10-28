using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Autonogrammer.UI.Components;

namespace Autonogrammer.UI.Tests;

/// <summary>A component over <see cref="ObservableComponent"/> that renders its model's value and counts its renders.</summary>
internal sealed class ObservableProbe : ObservableComponent
{
    private ObservableProbeModel? _watched;

    /// <summary>Gets or sets the model to render.</summary>
    [Parameter]
    public ObservableProbeModel? Model { get; set; }

    /// <summary>Gets the number of times the component has rendered.</summary>
    public int RenderCount { get; private set; }

    /// <summary>Creates a probe the way the renderer does, with no parameters set.</summary>
    /// <returns>The probe.</returns>
    internal static ObservableProbe Create() => new();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_watched, Model))
        {
            Observe(Model);
            return;
        }

        StopObserving(_watched);
        _watched = Model;
        Observe(Model);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        RenderCount++;
        builder.AddContent(0, Model?.Value);
    }
}
