namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Choice on the local approval dialog. Deny is the default. No timer ever allows.</summary>
public enum ComputerApprovalChoice
{
    /// <summary>The user denied, or the dialog timed out.</summary>
    Deny = 0,

    /// <summary>Allow this action once.</summary>
    AllowOnce = 1,

    /// <summary>Allow this class for the rest of the session.</summary>
    AllowForSession = 2,
}

