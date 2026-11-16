namespace Autonogrammer.Presentation.Landing;

/// <summary>The step a managed install of omp is in, as the landing shows its progress.</summary>
public enum LocalOmpInstallStage
{
    /// <summary>The release to install is being read from the registry.</summary>
    ReadingRelease = 0,

    /// <summary>The release is being installed and checked.</summary>
    Installing = 1,
}
