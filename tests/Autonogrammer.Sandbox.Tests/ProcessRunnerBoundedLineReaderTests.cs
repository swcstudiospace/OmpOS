using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Verifies the bounded streaming line reader matches ReadLineAsync terminator semantics
/// (CR, LF and CRLF, including a CRLF split across chunks) with a controllable chunk source.
/// </summary>
public sealed class ProcessRunnerBoundedLineReaderTests
{
    /// <summary>
    /// A TextReader that returns one configured piece per read, so chunk boundaries are exact.
    /// </summary>
    private sealed class ChunkedReader : TextReader
    {
        private readonly Queue<string> _chunks;

        public ChunkedReader(params string[] chunks)
        {
            _chunks = new Queue<string>(chunks);
        }

        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (_chunks.Count == 0)
            {
                return ValueTask.FromResult(0);
            }

            string chunk = _chunks.Dequeue();
            chunk.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(chunk.Length);
        }
    }

    private static async Task<List<string>> ReadAllAsync(TextReader source, CancellationToken cancellationToken)
    {
        var reader = new ProcessRunner.BoundedLineReader(source);
        var lines = new List<string>();
        while (true)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(true);
            if (line is null)
            {
                break;
            }

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// "a\rb\r\nc\n" delivered as "a\r", "b\r", "\nc\n" yields "a", "b", "c": a CR ends a line,
    /// and a CR at the end of one chunk plus an LF at the start of the next form a single terminator.
    /// </summary>
    [Fact]
    public async Task SplitCrlfAcrossChunks_YieldsThreeLinesWithoutCarriageReturns()
    {
        var ct = TestContext.Current.CancellationToken;
        var lines = await ReadAllAsync(new ChunkedReader("a\r", "b\r", "\nc\n"), ct).ConfigureAwait(true);

        lines.Count.ShouldBe(3);
        lines[0].ShouldBe("a");
        lines[1].ShouldBe("b");
        lines[2].ShouldBe("c");
        lines.ShouldAllBe(static line => !line.Contains('\r'));
    }

    /// <summary>
    /// A bare CR at end of input terminates the final line, matching ReadLineAsync.
    /// </summary>
    [Fact]
    public async Task BareCarriageReturnAtEndOfInput_TerminatesFinalLine()
    {
        var ct = TestContext.Current.CancellationToken;
        var lines = await ReadAllAsync(new ChunkedReader("x\r"), ct).ConfigureAwait(true);

        lines.Count.ShouldBe(1);
        lines[0].ShouldBe("x");
    }
}
