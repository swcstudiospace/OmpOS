using System.Text;
using Autonogrammer.Harness.Remote;

namespace Autonogrammer.Harness.RemoteProvision.Tests;

/// <summary>
/// A transport that never starts ssh. A call with no stdin is a read and returns the next queued probe answer. A call with stdin is a write: its
/// script is recorded and the next queued write answer is returned, or success when none is queued. Writes are what the tests count.
/// </summary>
internal sealed class FakeRemoteExec : IRemoteExec
{
    private readonly Queue<RemoteExecResult> _reads = new();
    private readonly Queue<RemoteExecResult> _writeAnswers = new();

    /// <summary>Gets the scripts that were written, in order, as the text the remote shell received.</summary>
    public List<string> WrittenScripts { get; } = [];

    /// <summary>Gets the number of read calls made.</summary>
    public int ReadCount { get; private set; }

    /// <summary>Gets the number of write calls made.</summary>
    public int WriteCount => WrittenScripts.Count;

    /// <inheritdoc />
    public string WorkspaceRoot => "/work";

    /// <summary>Queues the answer of the next read.</summary>
    /// <param name="result">The answer.</param>
    public void EnqueueRead(RemoteExecResult result) => _reads.Enqueue(result);

    /// <summary>Queues the answer of the next write.</summary>
    /// <param name="result">The answer.</param>
    public void EnqueueWriteAnswer(RemoteExecResult result) => _writeAnswers.Enqueue(result);

    /// <inheritdoc />
    public Task<RemoteExecResult> RunAsync(string script, ReadOnlyMemory<byte> input, int maxOutputBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input.IsEmpty)
        {
            ReadCount++;
            return Task.FromResult(_reads.Dequeue());
        }

        WrittenScripts.Add(Encoding.UTF8.GetString(input.Span));
        return Task.FromResult(_writeAnswers.Count > 0 ? _writeAnswers.Dequeue() : Ok(string.Empty));
    }

    /// <inheritdoc />
    public Task<string?> ResolveHomeDirectoryAsync(CancellationToken cancellationToken) => Task.FromResult<string?>("/home/tester");

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>Returns a successful answer with the given stdout.</summary>
    /// <param name="stdout">The stdout text.</param>
    /// <returns>The answer.</returns>
    public static RemoteExecResult Ok(string stdout) => new(0, Encoding.UTF8.GetBytes(stdout), string.Empty, OutputTruncated: false, TimedOut: false, StartFailure: null);

    /// <summary>Returns a failed script answer with the given exit code and stderr.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="stderr">The stderr text.</param>
    /// <returns>The answer.</returns>
    public static RemoteExecResult Failed(int exitCode, string stderr) => new(exitCode, ReadOnlyMemory<byte>.Empty, stderr, OutputTruncated: false, TimedOut: false, StartFailure: null);
}
