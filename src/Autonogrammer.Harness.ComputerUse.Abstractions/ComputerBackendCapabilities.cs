namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Optional facts a backend can report. Absent flags are off, including focused-element inspection in v0.16.0.</summary>
[System.Flags]
public enum ComputerBackendCapabilities
{
    /// <summary>The backend reports no optional capability.</summary>
    None = 0,

    /// <summary>The backend can list windows.</summary>
    WindowEnumeration = 1,

    /// <summary>The backend can inspect the focused element. Off on every backend in v0.16.0.</summary>
    FocusedElementInspection = 2,

    /// <summary>The backend can read and write clipboard text. The head still prefers <see cref="IClipboardTextPort"/>.</summary>
    ClipboardText = 4,
}
