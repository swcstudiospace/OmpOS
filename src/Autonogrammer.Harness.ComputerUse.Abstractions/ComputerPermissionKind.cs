namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Operating-system permission Computer Use may need before it arms.</summary>
public enum ComputerPermissionKind
{
    /// <summary>Screen capture permission (macOS Screen Recording). Message id <c>computer.permission.screen-recording</c>.</summary>
    ScreenRecording = 0,

    /// <summary>Input posting permission (macOS Accessibility). Message id <c>computer.permission.accessibility</c>.</summary>
    Accessibility = 1,
}

