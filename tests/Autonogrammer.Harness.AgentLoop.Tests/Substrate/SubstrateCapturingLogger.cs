using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Harness.AgentLoop.Tests.Substrate;

/// <summary>Records every log write, including the formatted message and any exception text, so tests can prove a secret never reaches a log.</summary>
internal sealed class SubstrateCapturingLogger : ILogger
{
    private readonly Lock _gate = new();
    private readonly List<string> _lines = [];

    /// <summary>Gets the recorded log lines: level, formatted message and exception text.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_gate)
            {
                return [.. _lines];
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
        var line = string.Create(CultureInfo.InvariantCulture, $"{logLevel}|{eventId.Id}|{formatter(state, exception)}|{exception}");
        lock (_gate)
        {
            _lines.Add(line);
        }
    }
}
