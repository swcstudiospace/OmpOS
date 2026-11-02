namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>The panel settings the host binds from <c>Cowork:Artifacts</c>.</summary>
/// <param name="AutoOpen">Whether the first artifact opens the panel (<c>Cowork:Artifacts:AutoOpen</c>).</param>
public sealed record ArtifactPanelOptions(bool AutoOpen);
