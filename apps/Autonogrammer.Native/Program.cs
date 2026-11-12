using Avalonia;

namespace Autonogrammer.Native;

/// <summary>
/// The single composition root of the native head. It builds the Avalonia application and starts it on the
/// classic desktop lifetime. OQ-33 names <c>StartWithClassicDesktopLifetime</c>; the package documentation for 12.1.4 confirms it.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The tool kinds the production approval policy admits. The owner's answer of 2026-10-24 extends the named set (Phase 21,
    /// decision F1, which first admitted only the kinds that write files or run commands) to <c>read</c> and <c>search</c>. The kinds
    /// come from the ACP tool-kind list (https://agentclientprotocol.com/protocol/v1/tool-calls): <c>edit</c>, <c>delete</c>,
    /// <c>move</c>, <c>execute</c>, <c>read</c> and <c>search</c>. The kinds <c>fetch</c>, <c>think</c>, <c>switch_mode</c> and
    /// <c>other</c> stay denied: each is refused before it reaches the approval UI.
    /// </summary>
    internal static readonly string[] ProductionAllowedKinds = ["edit", "delete", "move", "execute", "read", "search"];

    /// <summary>
    /// Starts the native desktop head. The Agent Panel opens at once in its Loading state; the plugins and the saved sessions are loaded
    /// behind it, and no <c>omp</c> process starts until a prompt is sent.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the desktop lifetime.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Builds the application with the windowing backend the process selects. The default is the platform detection the head has always
    /// used. The native Wayland backend is an explicit opt-in through <see cref="BackendSelection"/>. The head of the Agent Panel (the engines
    /// of N sessions, the registry and the panel) is composed when the main window opens, from the process environment, with the services' log
    /// output going to <see cref="System.Diagnostics.Trace"/>, so the stderr listener installed at startup receives it (ADR-0011).
    /// </summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure(() => new App());
        return BackendSelection.Select(Environment.GetEnvironmentVariable(BackendSelection.Variable), OperatingSystem.IsLinux()) switch
        {
            NativeBackend.Wayland => builder.UseWayland().UseSkia().UseHarfBuzz(),
            _ => builder.UsePlatformDetect(),
        };
    }

    /// <summary>The windowing backend the head starts on.</summary>
    internal enum NativeBackend
    {
        /// <summary>The platform detection the head has always used (X11 on Linux, which runs through Xwayland on a Wayland session).</summary>
        Default,

        /// <summary>The native Wayland backend of <c>Avalonia.Wayland</c>. It has no AT-SPI, so it has no screen-reader support on Linux.</summary>
        Wayland,
    }

    /// <summary>
    /// Chooses the windowing backend from <c>AUTONOGRAMMER_NATIVE_BACKEND</c>. Unset or empty selects the default. <c>wayland</c> (any case)
    /// selects the native Wayland backend, and only on Linux. Any other value is refused, so a typo never starts the wrong backend silently.
    /// </summary>
    internal static class BackendSelection
    {
        /// <summary>The variable that names the windowing backend.</summary>
        internal const string Variable = "AUTONOGRAMMER_NATIVE_BACKEND";

        /// <summary>Selects the backend from the value of <see cref="Variable"/>.</summary>
        /// <param name="value">The variable's value, or null when it is unset.</param>
        /// <param name="isLinux">Whether the head runs on Linux, the only platform the Wayland backend is built for.</param>
        /// <returns>The selected backend.</returns>
        /// <exception cref="InvalidOperationException">The value names an unknown backend, or Wayland is named off Linux.</exception>
        internal static NativeBackend Select(string? value, bool isLinux)
        {
            var name = value?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return NativeBackend.Default;
            }

            if (!string.Equals(name, "wayland", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{Variable} names an unknown backend. The only value is 'wayland'; unset it for the default backend.");
            }

            if (!isLinux)
            {
                throw new InvalidOperationException($"{Variable}=wayland applies to Linux only.");
            }

            return NativeBackend.Wayland;
        }
    }
}
