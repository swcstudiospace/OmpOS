using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>A failed start surfaces its own exception even when the compensating dispose also fails.</summary>
public sealed class RpcClientStartTests
{
    /// <summary>
    /// The start failure is rethrown unchanged (same instance) while the dispose failure is swallowed.
    /// </summary>
    [Fact]
    [SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "Ownership transfers to StartSessionAsync, which disposes the session when start fails.")]
    public async Task FailedStartRethrowsOriginalWhenCompensatingDisposeFails()
    {
        var sentinel = new RpcCommandException("prompt", "busy", "omp refused the prompt.", "omp rpc");
        var session = new OmpRpcSession(
            new FailingDisposeTransport(), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        RpcCommandException thrown = await Should.ThrowAsync<RpcCommandException>(
            () => OmpRpcClient.StartSessionAsync(session, _ => Task.FromException(sentinel))).ConfigureAwait(true);

        thrown.ShouldBeSameAs(sentinel);
    }

    /// <summary>A successful start returns the session it started.</summary>
    [Fact]
    [SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "The test disposes the session it started at scope end.")]
    public async Task SuccessfulStartReturnsSession()
    {
        var session = new OmpRpcSession(
            new FakeRpcTransport(), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        IOmpSession started = await OmpRpcClient.StartSessionAsync(session, _ => Task.CompletedTask)
            .ConfigureAwait(true);

        started.ShouldBeSameAs(session);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>An <see cref="IRpcTransport"/> whose dispose always fails, for the compensating-dispose path.</summary>
    private sealed class FailingDisposeTransport : IRpcTransport
    {
        private readonly Channel<string> _stdout = Channel.CreateUnbounded<string>();

        /// <inheritdoc />
        public IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken) =>
            _stdout.Reader.ReadAllAsync(cancellationToken);

        /// <inheritdoc />
        public Task SendLineAsync(string line, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task CloseInputAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        /// <inheritdoc />
        public ValueTask DisposeAsync() =>
            throw new InvalidOperationException("The test transport failed to dispose.");
    }
}
