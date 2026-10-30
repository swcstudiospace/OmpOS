namespace Autonogrammer.Parity.Support;

/// <summary>
/// One normalized block of a session-chat render: its kind from <see cref="ParityKinds"/> and its visible text
/// after <see cref="ParityNormalization.CollapseWhitespace"/>.
/// </summary>
/// <param name="Kind">The normalized block kind.</param>
/// <param name="Text">The visible text of the block, whitespace collapsed.</param>
public sealed record ParityBlock(string Kind, string Text);
