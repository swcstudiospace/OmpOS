using Microsoft.Extensions.Logging;

namespace Autonogrammer.Coordination.Tests;

/// <summary>Records every log write so tests can assert on the level operators would see.</summary>
/// <typeparam name="T">The category type.</typeparam>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly System.Threading.Lock _gate = new();
    private readonly List<LogEntry> _entries = [];

    /// <summary>Gets a snapshot of the recorded log writes in write order. Writes arrive from the threads of the code under test, so the snapshot is taken under a lock.</summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var entry = new LogEntry(logLevel, eventId, formatter(state, exception), exception);
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    /// <summary>One recorded log write.</summary>
    /// <param name="Level">The log level.</param>
    /// <param name="EventId">The event id.</param>
    /// <param name="Message">The formatted message.</param>
    /// <param name="Exception">The exception logged with the write, if any.</param>
    internal sealed record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);
}
