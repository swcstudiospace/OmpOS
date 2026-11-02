using Autonogrammer.Presentation.Cowork.Artifacts;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>Carries the outcome of one Access review. The host applies it with <see cref="ArtifactPanelModel.ApplyAsync"/>.</summary>
public sealed class ArtifactAccessDecisionEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ArtifactAccessDecisionEventArgs"/> class.</summary>
    /// <param name="decision">The decision the review produced.</param>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is null.</exception>
    public ArtifactAccessDecisionEventArgs(ArtifactAccessDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        Decision = decision;
    }

    /// <summary>Gets the decision.</summary>
    public ArtifactAccessDecision Decision { get; }
}
