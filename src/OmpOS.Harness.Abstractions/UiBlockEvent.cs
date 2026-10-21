using OmpOS.Contracts.Ui;

namespace OmpOS.Harness.Abstractions;

/// <summary>A Generative UI block to render.</summary>
/// <param name="Block">The UI block.</param>
public sealed record UiBlockEvent(UiBlock Block) : HarnessEvent;
