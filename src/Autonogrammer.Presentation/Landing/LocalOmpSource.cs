namespace Autonogrammer.Presentation.Landing;

/// <summary>Where the omp that runs on this computer came from.</summary>
public enum LocalOmpSource
{
    /// <summary>The managed install.</summary>
    Managed = 0,

    /// <summary>The absolute path set in configuration.</summary>
    Configured = 1,

    /// <summary>A PATH directory or a known install location.</summary>
    Path = 2,
}
