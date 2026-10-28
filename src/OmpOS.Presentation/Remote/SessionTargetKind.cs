namespace OmpOS.Presentation.Remote;

/// <summary>Where a new session runs, as the picker offers it.</summary>
public enum SessionTargetKind
{
    /// <summary>On this computer.</summary>
    Local,

    /// <summary>On a machine reached over ssh.</summary>
    Ssh,

    /// <summary>On a cloud worker.</summary>
    Cloud,
}
