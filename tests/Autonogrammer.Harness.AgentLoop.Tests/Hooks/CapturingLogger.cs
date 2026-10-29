using Autonogrammer.Harness.AgentLoop.Hooks;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// A logger that keeps every rendered message, prefixed with its level, and lets a test wait for a message instead of waiting for
/// time to pass. The server logs the end of every request, so a test can wait for "this connection is finished".
/// </summary>
internal sealed class CapturingLogger : ILogger<HookBridgeServer>
{
    private readonly Lock _gate = new();
    private readonly List<string> _messages = [];
    private readonly List<(Func<string, bool> Predicate, TaskCompletionSource Signal)> _waiters = [];

    /// <summary>Gets a snapshot of the messages logged so far, each as <c>Level: text</c>.</summary>
    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_gate)
            {
                return [.. _messages];
            }
        }
    }

    /// <summary>Returns a task that completes when a message that satisfies the predicate has been logged, now or later.</summary>
    /// <param name="predicate">Tests a rendered message.</param>
    /// <returns>The task.</returns>
    public Task WaitForAsync(Func<string, bool> predicate)
    {
        lock (_gate)
        {
            if (_messages.Any(predicate))
            {
                return Task.CompletedTask;
            }

            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((predicate, signal));
            return signal.Task;
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
        var message = logLevel + ": " + formatter(state, exception);
        lock (_gate)
        {
            _messages.Add(message);
            foreach (var waiter in _waiters.Where(waiter => waiter.Predicate(message)).ToList())
            {
                waiter.Signal.TrySetResult();
                _waiters.Remove(waiter);
            }
        }
    }
}
