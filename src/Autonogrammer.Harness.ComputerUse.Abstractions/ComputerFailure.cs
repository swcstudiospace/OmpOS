namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Failure category for <see cref="ComputerBackendException"/>. Messages never carry titles or typed text.</summary>
public enum ComputerFailure
{
    /// <summary>An operating-system permission is missing.</summary>
    PermissionMissing = 0,

    /// <summary>The backend cannot perform the call.</summary>
    Unavailable = 1,

    /// <summary>The target window is gone.</summary>
    TargetGone = 2,

    /// <summary>The target process is elevated above this process.</summary>
    Elevated = 3,

    /// <summary>The secure desktop is active.</summary>
    SecureDesktop = 4,

    /// <summary>The operating-system call timed out.</summary>
    Timeout = 5,
}

