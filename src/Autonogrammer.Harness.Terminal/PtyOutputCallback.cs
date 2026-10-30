namespace Autonogrammer.Harness.Terminal;

/// <summary>
/// Receives one piece of decoded terminal output. The span is valid only for the duration of the call.
/// </summary>
/// <param name="text">The decoded text. It is never empty.</param>
internal delegate void PtyOutputCallback(ReadOnlySpan<char> text);
