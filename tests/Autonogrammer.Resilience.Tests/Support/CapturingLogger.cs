using Microsoft.Extensions.Logging;

namespace Autonogrammer.Resilience.Tests;

/// <summary>Records every log write so a test can assert on the level and text an operator would see.</summary>
internal sealed class CapturingLogger : ILogger<ResiliencePipeline>
{
    private readonly System.Threading.Lock _gate = new();
    private readonly List<LogEntry> _entries = [];

    /// <summary>Gets a snapshot of the recorded log writes in write order.</summary>
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
        ArgumentNullException.ThrowIfNull(formatter);
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
