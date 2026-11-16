using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Cowork.Artifacts.Stores.Tests.Support;

/// <summary>A logger factory that keeps every message the Npgsql driver logs, so a test can see which commands ran.</summary>
internal sealed class CommandTextLoggerFactory : ILoggerFactory
{
    private readonly ConcurrentQueue<string> _messages = new();

    /// <summary>Gets the messages logged so far, oldest first.</summary>
    public IReadOnlyList<string> Messages => [.. _messages];

    /// <summary>Forgets the messages logged so far.</summary>
    public void Clear() => _messages.Clear();

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new Capture(_messages);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class Capture(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            messages.Enqueue(formatter(state, exception));
        }
    }
}
