using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Capturing log sink for redaction and scope tests. Every formatted message lands in <see cref="Messages"/>, and every entry
/// lands in <see cref="Entries"/> with the log scope states active when it was written. Scopes follow the async flow, so an
/// entry written while a request is handled carries that request's scopes.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentBag<string> _messages = new();
    private readonly ConcurrentBag<CapturedLogEntry> _entries = new();
    private readonly AsyncLocal<ImmutableStack<object>?> _scopes = new();

    /// <summary>Gets the captured messages.</summary>
    public IReadOnlyCollection<string> Messages => _messages;

    /// <summary>Gets the captured entries, each with the scope states that were active when it was written.</summary>
    public IReadOnlyCollection<CapturedLogEntry> Entries => _entries;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        return new CapturingLogger(categoryName, _messages, _entries, _scopes);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>A logger writing formatted messages and scoped entries into shared sinks, and recording scopes it begins.</summary>
    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentBag<string> _messages;
        private readonly ConcurrentBag<CapturedLogEntry> _entries;
        private readonly AsyncLocal<ImmutableStack<object>?> _scopes;

        /// <summary>Initializes a new instance of the <see cref="CapturingLogger"/> class.</summary>
        /// <param name="category">The logger category.</param>
        /// <param name="messages">The shared message sink.</param>
        /// <param name="entries">The shared entry sink.</param>
        /// <param name="scopes">The per-async-flow scope stack shared by the provider's loggers.</param>
        public CapturingLogger(string category, ConcurrentBag<string> messages, ConcurrentBag<CapturedLogEntry> entries, AsyncLocal<ImmutableStack<object>?> scopes)
        {
            _category = category;
            _messages = messages;
            _entries = entries;
            _scopes = scopes;
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            var previous = _scopes.Value;
            _scopes.Value = (previous ?? ImmutableStack<object>.Empty).Push(state);
            return new ScopeRestore(_scopes, previous);
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            var message = formatter(state, exception);
            _messages.Add($"{_category} {logLevel}: {message}");
            var scopes = (_scopes.Value ?? ImmutableStack<object>.Empty).Reverse().ToArray();
            _entries.Add(new CapturedLogEntry(_category, logLevel, message, scopes));
        }

        /// <summary>Restores the scope stack that was current before a scope began.</summary>
        private sealed class ScopeRestore : IDisposable
        {
            private readonly AsyncLocal<ImmutableStack<object>?> _scopes;
            private readonly ImmutableStack<object>? _previous;

            /// <summary>Initializes a new instance of the <see cref="ScopeRestore"/> class.</summary>
            /// <param name="scopes">The shared scope stack.</param>
            /// <param name="previous">The stack to restore on disposal.</param>
            public ScopeRestore(AsyncLocal<ImmutableStack<object>?> scopes, ImmutableStack<object>? previous)
            {
                _scopes = scopes;
                _previous = previous;
            }

            /// <inheritdoc />
            public void Dispose() => _scopes.Value = _previous;
        }
    }
}
