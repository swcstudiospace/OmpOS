using Autonogrammer.Harness.Terminal;

namespace Autonogrammer.Native.Views.Terminal;

/// <summary>The session the user types into after it changed.</summary>
internal sealed class OmpTerminalSessionEventArgs(ITerminalSession? session) : EventArgs
{
    /// <summary>Gets the running session, or null when omp ended or was stopped.</summary>
    public ITerminalSession? Session { get; } = session;
}
