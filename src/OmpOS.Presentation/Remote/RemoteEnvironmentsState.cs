namespace OmpOS.Presentation.Remote;

/// <summary>What the remote environments page shows as a whole.</summary>
public enum RemoteEnvironmentsState
{
    /// <summary>The saved environments are being read.</summary>
    Loading,

    /// <summary>No environment is saved.</summary>
    Empty,

    /// <summary>At least one environment is saved.</summary>
    Ready,

    /// <summary>The saved environments could not be read.</summary>
    Error,
}
