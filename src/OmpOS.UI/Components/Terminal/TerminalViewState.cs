namespace OmpOS.UI.Components.Terminal;

/// <summary>Where the omp terminal view stands. The view shows exactly one state at a time.</summary>
public enum TerminalViewState
{
    /// <summary>The stream is being opened; no output has arrived yet.</summary>
    Connecting,

    /// <summary>The stream is open and output is arriving.</summary>
    Live,

    /// <summary>The stream dropped and the view is attaching again after the last accepted chunk.</summary>
    Reconnecting,

    /// <summary>The terminal finished, or the connection could not be restored.</summary>
    Ended,

    /// <summary>The head cannot attach to a terminal; the view shows the reason.</summary>
    Unavailable,
}
