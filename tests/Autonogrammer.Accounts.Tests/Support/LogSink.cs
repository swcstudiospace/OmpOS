using System.Collections.Concurrent;

namespace Autonogrammer.Accounts.Tests.Support;

/// <summary>Collects every log line, with its event id and rendered message, from every <see cref="CapturingLogger{T}"/> that shares it.</summary>
internal sealed class LogSink
{
    private readonly ConcurrentQueue<string> _lines = new();

    /// <summary>Gets the lines captured so far.</summary>
    public IReadOnlyList<string> Lines => [.. _lines];

    /// <summary>Adds a line.</summary>
    /// <param name="line">The line.</param>
    public void Add(string line) => _lines.Enqueue(line);
}
