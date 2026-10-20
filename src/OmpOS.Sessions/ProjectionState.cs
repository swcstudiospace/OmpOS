using OmpOS.Contracts.Ui;

namespace OmpOS.Sessions;

/// <summary>
/// The UI state derived from a session's event stream by <see cref="HarnessEventProjection"/>.
/// </summary>
/// <remarks>
/// <see cref="Blocks"/> is the ordered list of rendered UI blocks. <see cref="StreamingText"/> is the
/// text buffer of the assistant message currently streaming; it is flushed into a
/// <see cref="MarkdownBlock"/> when the next block-producing event arrives or the prompt completes.
/// Collection properties use reference equality in record comparison, so compare
/// two states by their fields or by serialized form.
/// </remarks>
public sealed record ProjectionState
{
    /// <summary>Gets the empty starting state.</summary>
    public static ProjectionState Empty { get; } = new();

    /// <summary>Gets the ordered UI blocks.</summary>
    public IReadOnlyList<UiBlock> Blocks { get; init; } = [];

    /// <summary>Gets the streamed text not yet flushed into a markdown block.</summary>
    public string StreamingText { get; init; } = string.Empty;

    /// <summary>Gets the completion reason once the prompt has completed; <see langword="null"/> while running.</summary>
    public string? CompletionReason { get; init; }
}
