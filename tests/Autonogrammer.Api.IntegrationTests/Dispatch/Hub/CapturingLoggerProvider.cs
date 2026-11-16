using Microsoft.Extensions.Logging;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>A logger provider that keeps every formatted log line and exception text, so a test can search the whole log for a secret.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _lines = [];

    /// <summary>Gets every captured line.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lines)
            {
                return _lines.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    internal void Add(string line)
    {
        lock (_lines)
        {
            _lines.Add(line);
        }
    }

    private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception) + " " + (state?.ToString() ?? string.Empty);
            owner.Add(exception is null ? text : text + " " + exception);
        }
    }
}
