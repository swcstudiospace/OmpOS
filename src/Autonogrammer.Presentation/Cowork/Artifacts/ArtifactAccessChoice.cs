namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>The three answers the Access review offers (D-26).</summary>
public enum ArtifactAccessChoice
{
    /// <summary>Approves the origins for this panel session, in memory only.</summary>
    AllowSession,

    /// <summary>Approves the origins for this exact version (artifact, number and hash).</summary>
    AllowVersion,

    /// <summary>Approves nothing. Focus starts here, and Escape chooses it.</summary>
    Deny,
}
