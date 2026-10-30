namespace Autonogrammer.Presentation.About;

/// <summary>One address the About window offers to open in the system browser.</summary>
/// <param name="Label">The visible label.</param>
/// <param name="Location">The absolute https address.</param>
/// <param name="AccessibleName">The name a screen reader announces for the control that opens it.</param>
public sealed record AboutLink(string Label, string Location, string AccessibleName);
