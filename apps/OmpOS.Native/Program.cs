using Avalonia;
using OmpOS.Native.Engine;
using OmpOS.Native.Logging;

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

    /// <summary>Starts the native desktop head. The engine is composed on the first send or decision, never at launch.</summary>
    /// <param name="args">Command-line arguments passed to the desktop lifetime.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp(ComposeProductionEngine).StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Composes the production engine: the named-kind approval policy over <c>omp acp</c> in the working directory. The services'
    /// log output goes to <see cref="System.Diagnostics.Trace"/>, so the stderr listener installed at startup receives it (ADR-0011).
    /// </summary>
    /// <returns>The composed engine. It starts no process until a prompt is submitted.</returns>
    internal static EngineComposition ComposeProductionEngine() => EngineComposition.Compose(ProductionAllowedKinds, loggerFactory: DesktopLogging.Factory);

    /// <summary>Builds the application with the windowing platform detected at run time and no engine configured.</summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(composeEngine: null);

    /// <summary>
    /// Builds the application with the windowing platform detected at run time. When an engine is configured,
    /// <paramref name="composeEngine"/> runs on the first tool-approval decision, never at launch.
    /// </summary>
    /// <param name="composeEngine">Composes the engine on first use. Null means no engine is configured, and approvals stay unconnected.</param>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp(Func<EngineComposition>? composeEngine)
    {
        if (composeEngine is null)
        {
            return AppBuilder.Configure<App>().UsePlatformDetect();
        }

        var engine = new Lazy<EngineComposition>(composeEngine);
        return AppBuilder.Configure(() => new App(engine)).UsePlatformDetect();
    }
}
