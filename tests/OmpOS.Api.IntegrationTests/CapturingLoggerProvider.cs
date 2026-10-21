using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Capturing log sink for redaction tests: every formatted message lands in <see cref="Messages"/>.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentBag<string> _messages = new();

    /// <summary>Gets the captured messages.</summary>
    public IReadOnlyCollection<string> Messages => _messages;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        return new CapturingLogger(categoryName, _messages);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>A logger writing formatted messages into a shared bag.</summary>
    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentBag<string> _messages;

        /// <summary>Initializes a new instance of the <see cref="CapturingLogger"/> class.</summary>
        /// <param name="category">The logger category.</param>
        /// <param name="messages">The shared sink.</param>
        public CapturingLogger(string category, ConcurrentBag<string> messages)
        {
            _category = category;
            _messages = messages;
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
            _messages.Add($"{_category} {logLevel}: {formatter(state, exception)}");
        }
    }
}
