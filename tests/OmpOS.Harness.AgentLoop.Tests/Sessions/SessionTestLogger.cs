using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OmpOS.Harness.AgentLoop.Tests.Sessions;

/// <summary>A logger that keeps every formatted message, with its exception text, so a test can assert on what was logged.</summary>
internal sealed class SessionTestLogger : ILogger
{
    private readonly ConcurrentQueue<string> _messages = new();

    /// <summary>Gets every message logged so far.</summary>
    public IReadOnlyList<string> Messages => [.. _messages];

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _messages.Enqueue(exception is null ? formatter(state, exception) : formatter(state, exception) + " | " + exception);
    }
}
