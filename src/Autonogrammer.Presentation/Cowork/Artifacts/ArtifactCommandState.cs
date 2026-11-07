namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>A toolbar command and whether it is enabled for the selected artifact.</summary>
/// <param name="Command">The command.</param>
/// <param name="IsEnabled">Whether the command can run now.</param>
public sealed record ArtifactCommandState(ArtifactCommand Command, bool IsEnabled);
