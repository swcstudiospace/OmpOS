namespace OmpOS.Harness.Abstractions;

/// <summary>Options for a single prompt submission.</summary>
public sealed record PromptOptions
{
    /// <summary>Gets a value indicating whether the prompt steers an in-flight turn instead of queuing. Defaults to <see langword="false"/>.</summary>
    public bool Steer { get; init; }
}
