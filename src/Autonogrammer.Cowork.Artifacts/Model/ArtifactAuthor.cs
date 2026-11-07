namespace Autonogrammer.Cowork.Artifacts.Model;

/// <summary>Who wrote a version. The host sets this; callers cannot spoof it past the service.</summary>
public enum ArtifactAuthor
{
    /// <summary>The model or host tool wrote the version.</summary>
    Agent,

    /// <summary>A person saved an edit.</summary>
    User,
}
