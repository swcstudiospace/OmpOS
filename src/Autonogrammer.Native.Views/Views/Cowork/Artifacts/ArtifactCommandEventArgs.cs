using Autonogrammer.Presentation.Cowork.Artifacts;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>Carries a toolbar command the artifact panel raised. The host runs the command against the models.</summary>
public sealed class ArtifactCommandEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ArtifactCommandEventArgs"/> class.</summary>
    /// <param name="command">The command the user chose.</param>
    public ArtifactCommandEventArgs(ArtifactCommand command)
    {
        Command = command;
    }

    /// <summary>Gets the command the user chose.</summary>
    public ArtifactCommand Command { get; }
}
