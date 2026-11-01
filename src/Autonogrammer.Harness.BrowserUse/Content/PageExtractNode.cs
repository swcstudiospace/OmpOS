namespace Autonogrammer.Harness.BrowserUse.Content;

/// <summary>One DOM or accessibility node the extractor may keep or drop.</summary>
/// <param name="Text">The text.</param>
/// <param name="Visible">Whether the visibility filter keeps the node.</param>
/// <param name="Kind">Where the text came from.</param>
/// <param name="Sensitive">Whether the value must be masked.</param>
public sealed record PageExtractNode(string Text, bool Visible, PageTextKind Kind, bool Sensitive = false);
