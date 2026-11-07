namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>The outcome of one Access review.</summary>
/// <param name="Choice">The answer, or <see cref="ArtifactAccessChoice.Deny"/> for a timeout or Escape.</param>
/// <param name="Version">The version the review was opened for.</param>
/// <param name="Origins">The origins approved. Empty for a denial.</param>
/// <param name="Reason">Why the decision was made: the choice, Escape or a timeout.</param>
public sealed record ArtifactAccessDecision(
    ArtifactAccessChoice Choice,
    ArtifactVersionKey Version,
    IReadOnlyList<string> Origins,
    string Reason);
