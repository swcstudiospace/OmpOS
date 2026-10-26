using Avalonia;
using OmpOS.Native.Agents;

namespace OmpOS.Native;

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
    /// Builds the application with the windowing platform detected at run time. The head of the Agent Panel (the engines of N sessions,
    /// the registry and the panel) is composed when the main window opens, from the process environment, with the services' log output
    /// going to <see cref="System.Diagnostics.Trace"/>, so the stderr listener installed at startup receives it (ADR-0011).
    /// </summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() => new App(AgentHead.CreateProduction)).UsePlatformDetect();
}
