namespace Autonogrammer.Presentation.Terminal;

/// <summary>The kind of head that hosts the terminal surface. Only <see cref="Desktop"/> may spawn a local shell.</summary>
public enum TerminalHostKind
{
    /// <summary>A desktop head (Windows, macOS or Linux) that owns its processes and may run a local shell.</summary>
    Desktop,

    /// <summary>A mobile head (Android or iOS). The platform sandbox forbids a general local shell, so none is spawned.</summary>
    Mobile,

    /// <summary>The WebAssembly head. A browser cannot spawn a process, so no local shell exists.</summary>
    Web,
}
