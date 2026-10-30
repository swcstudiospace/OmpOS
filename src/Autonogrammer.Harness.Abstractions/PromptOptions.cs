namespace Autonogrammer.Harness.Abstractions;

/// <summary>Options for a single prompt submission.</summary>
public sealed record PromptOptions
{
    /// <summary>Gets a value indicating whether the prompt steers an in-flight turn instead of queuing. Defaults to <see langword="false"/>.</summary>
    public bool Steer { get; init; }

    /// <summary>Gets the resolved @-mentions that travel with the prompt as embedded resources, in attachment order. Empty for a prompt without mentions.</summary>
    public IReadOnlyList<PromptResource> Resources { get; init; } = [];

    /// <summary>Gets the images that travel with the prompt as image content blocks, in attachment order. Empty for a prompt without images. An engine that does not take images (see <see cref="IOmpModelControl.SupportsImages"/>) leaves them out.</summary>
    public IReadOnlyList<PromptImage> Images { get; init; } = [];
}
