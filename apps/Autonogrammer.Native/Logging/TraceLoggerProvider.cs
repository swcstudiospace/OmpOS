using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> that forwards each entry to <see cref="Trace"/>, so the services' <see cref="ILogger"/> output
/// reaches the stderr trace listener the desktop app installs at startup (ADR-0011). Warning maps to
/// <see cref="Trace.TraceWarning(string)"/>, Error and Critical map to <see cref="Trace.TraceError(string)"/>, and every other level
/// is written with <see cref="Trace.WriteLine(string)"/>.
/// </summary>
internal sealed class TraceLoggerProvider : ILoggerProvider
{
    /// <summary>Creates a logger that writes entries of one category to <see cref="Trace"/>.</summary>
    /// <param name="categoryName">The category the entries are written under, normally the logging class's full name.</param>
    /// <returns>The logger.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="categoryName"/> is null.</exception>
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);

        return new TraceLogger(categoryName);
    }

    /// <summary>Releases nothing: the provider holds no resource, and <see cref="Trace"/> is owned by the process.</summary>
    public void Dispose()
    {
    }

    /// <summary>The logger of one category. Every level except <see cref="LogLevel.None"/> is enabled.</summary>
    /// <param name="category">The category the entries are written under.</param>
    private sealed class TraceLogger(string category) : ILogger
    {
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);

            var text = formatter(state, exception);
            var message = exception is null
                ? $"{category}: {text}"
                : $"{category}: {text}{Environment.NewLine}{exception}";

            switch (logLevel)
            {
                case LogLevel.Warning:
                    Trace.TraceWarning(message);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    Trace.TraceError(message);
                    break;
                default:
                    Trace.WriteLine(message);
                    break;
            }
        }
    }
}
