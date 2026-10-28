using Autonogrammer.Contracts.Ui;

namespace Autonogrammer.Contracts.Streaming;

/// <summary>
/// A Generative UI block carried by the stream (kind "ui_block").
/// </summary>
public sealed record UiBlockPayload : StreamEventPayload
{
    /// <summary>Gets the UI block.</summary>
    public required UiBlock Block { get; init; }
}
