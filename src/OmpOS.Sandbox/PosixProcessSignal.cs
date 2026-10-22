using System.Runtime.InteropServices;

namespace OmpOS.Sandbox;

/// <summary>
/// Minimal POSIX signal delivery used to freeze a process tree before it is killed.
/// </summary>
internal static class PosixProcessSignal
{
    /// <summary>
    /// SIGSTOP on Linux x86-64, AArch64 and 32-bit ARM, the platforms this repository builds for.
    /// </summary>
    internal const int SigStop = 19;

    [DllImport("libc", EntryPoint = "kill")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int NativeKill(int pid, int signal);

    /// <summary>
    /// Sends SIGSTOP to <paramref name="pid"/>. A stopped process cannot fork or exit on its own, so
    /// a tree that is stopped and then enumerated cannot grow while it is being killed.
    /// </summary>
    /// <param name="pid">The process id to stop.</param>
    /// <returns>True when the signal was delivered; false when the pid no longer exists.</returns>
    internal static bool TryStop(int pid) => NativeKill(pid, SigStop) == 0;
}
