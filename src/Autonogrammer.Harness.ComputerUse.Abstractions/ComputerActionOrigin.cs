namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Where a Computer Use action was requested.</summary>
public enum ComputerActionOrigin
{
    /// <summary>The local Cowork session.</summary>
    Local = 0,

    /// <summary>A Dispatch-origin session. Approvals still happen only on this desktop.</summary>
    Dispatch = 1,
}

