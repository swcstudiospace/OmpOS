namespace Autonogrammer.Presentation.Commands;

/// <summary>What the composer does with a send after the slash resolver and, for a native action, the head ran.</summary>
/// <param name="Kind">Whether to send a prompt or to stop.</param>
/// <param name="Text">The prompt to send, or the sentence for the status line (which may be empty).</param>
/// <param name="Resolution">How the typed text was resolved.</param>
public sealed record SlashDispatch(SlashDispatchKind Kind, string Text, SlashResolution Resolution);
