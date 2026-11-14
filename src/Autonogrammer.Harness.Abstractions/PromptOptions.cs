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

    /// <summary>
    /// Gets the action an engine calls once the prompt request has been written to its transport. It is not called when the prompt is refused,
    /// or cancelled, before the write. A caller uses it to tell a prompt that never left from one whose outcome is unknown. Null for no report.
    /// </summary>
    public Action? Transmitted { get; init; }

    /// <summary>
    /// Gets whether the prompt must be refused, before anything is sent, when the engine did not advertise embedded context and <see cref="Resources"/> is not empty.
    /// Ordinary mentions keep their behaviour when this is false. Defaults to <see langword="false"/>.
    /// </summary>
    public bool RequireEmbeddedContext { get; init; }
}
