using OmpOS.Contracts.Ui;

namespace OmpOS.GenUi;

/// <summary>
/// The immutable UI tree produced by folding a session's stream events.
/// </summary>
/// <remarks>
/// <see cref="Blocks"/> is the ordered list of rendered UI blocks. <see cref="StreamingText"/> is the
/// text buffer of the assistant message currently streaming; it is flushed into a
/// <see cref="MarkdownBlock"/> when the next block-producing event arrives or the prompt completes.
/// This type mirrors <c>ProjectionState</c> field-for-field so the <see cref="UiFolder"/> replay
/// produces the same block sequence as <c>HarnessEventProjection</c>, without depending on it.
/// </remarks>
public sealed record UiTree
{
    /// <summary>Gets the empty starting tree.</summary>
    public static UiTree Empty { get; } = new();

    /// <summary>Gets the ordered UI blocks.</summary>
    public IReadOnlyList<UiBlock> Blocks { get; init; } = [];

    /// <summary>Gets the streamed text not yet flushed into a markdown block.</summary>
    public string StreamingText { get; init; } = string.Empty;

    /// <summary>Gets the completion reason once the prompt has completed; <see langword="null"/> while running.</summary>
    public string? CompletionReason { get; init; }
}
