namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>One diff line. The host computes the lines; the model only presents them.</summary>
/// <param name="Change">The change.</param>
/// <param name="Text">The line text, shown as text only.</param>
public sealed record ArtifactDiffLine(ArtifactDiffChange Change, string Text);
