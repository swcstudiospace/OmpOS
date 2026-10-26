using Microsoft.Extensions.Logging;

namespace OmpOS.Native.Logging;

/// <summary>
/// The logger factory of the desktop composition root. Its only provider is <see cref="TraceLoggerProvider"/>, so every service the
/// desktop app composes logs to <see cref="System.Diagnostics.Trace"/>.
/// </summary>
internal static class DesktopLogging
{
    /// <summary>The one provider every factory shares. It holds no resource, so sharing it across factories is safe.</summary>
    private static readonly TraceLoggerProvider Provider = new();

    /// <summary>
    /// Gets the factory the desktop app composes its services with. It lives as long as the process and is created on first use, so
    /// launching the app creates no logger.
    /// </summary>
    internal static ILoggerFactory Factory { get; } = CreateFactory();

    /// <summary>Creates a factory whose entries of level Information and above go to <see cref="System.Diagnostics.Trace"/>.</summary>
    /// <returns>The factory. The caller owns it.</returns>
    internal static ILoggerFactory CreateFactory() =>
        new LoggerFactory([Provider], new LoggerFilterOptions { MinLevel = LogLevel.Information });
}
