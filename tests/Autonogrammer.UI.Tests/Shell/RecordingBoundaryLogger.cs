using System.Collections.Concurrent;
using Autonogrammer.UI.Components.Shell;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>A logger that records the level, the formatted message and the exception of every entry the error boundary writes.</summary>
internal sealed class RecordingBoundaryLogger : ILogger<AppErrorBoundary>
{
    /// <summary>Gets the entries in the order they were logged.</summary>
    internal ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Enqueue((logLevel, formatter(state, exception), exception));
    }
}
