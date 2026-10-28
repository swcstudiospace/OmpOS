namespace OmpOS.Harness.Abstractions;

/// <summary>Options for a single prompt submission.</summary>
public sealed record PromptOptions
{
    /// <summary>Gets a value indicating whether the prompt steers an in-flight turn instead of queuing. Defaults to <see langword="false"/>.</summary>
    public bool Steer { get; init; }

    /// <summary>Gets the resolved @-mentions that travel with the prompt as embedded resources, in attachment order. Empty for a prompt without mentions.</summary>
    public IReadOnlyList<PromptResource> Resources { get; init; } = [];
}
