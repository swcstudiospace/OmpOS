using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OmpOS.AgentWorker.Tests;

/// <summary>Records every formatted log message, with its category and exception text, so tests can prove what operators would see.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    /// <summary>Gets every captured line in write order.</summary>
    public IReadOnlyCollection<string> Lines => _lines;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _lines);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<string> _lines;

        public CapturingLogger(string category, ConcurrentQueue<string> lines)
        {
            _category = category;
            _lines = lines;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _lines.Enqueue(_category + " [" + logLevel + "] " + formatter(state, exception) + (exception is null ? string.Empty : " | " + exception));
        }
    }
}
