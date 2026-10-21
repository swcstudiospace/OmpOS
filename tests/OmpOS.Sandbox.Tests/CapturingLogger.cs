using Microsoft.Extensions.Logging;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Records every log write so tests can assert on the level and message operators would see.
/// </summary>
/// <typeparam name="T">The category type.</typeparam>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

    /// <summary>
    /// Gets the recorded log writes in write order.
    /// </summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
    }

    /// <summary>
    /// One recorded log write.
    /// </summary>
    /// <param name="Level">The log level.</param>
    /// <param name="EventId">The event id.</param>
    /// <param name="Message">The formatted message.</param>
    public sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
}
