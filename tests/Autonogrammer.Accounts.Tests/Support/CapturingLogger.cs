using Microsoft.Extensions.Logging;

namespace Autonogrammer.Accounts.Tests.Support;

/// <summary>A logger that records the event id, level, rendered message and exception text of everything written to it.</summary>
/// <typeparam name="T">The category type.</typeparam>
internal sealed class CapturingLogger<T>(LogSink sink) : ILogger<T>
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        string stateText = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? string.Join(';', pairs.Select(pair => pair.Key + "=" + pair.Value))
            : string.Empty;
        sink.Add($"{eventId.Id}|{logLevel}|{formatter(state, exception)}|{stateText}|{exception}");
    }
}
