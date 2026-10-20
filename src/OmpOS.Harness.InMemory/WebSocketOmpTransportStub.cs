using System.Runtime.CompilerServices;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.InMemory;

/// <summary>
/// Placeholder for the remote WebSocket ACP transport. Every operation fails with a typed
/// <see cref="ProcessStartHarnessException"/> whose inner exception is a
/// <see cref="NotImplementedException"/>. No protocol work exists; the owner is tracked in
/// docs/open-questions.md as Q-HARN-WS.
/// </summary>
public sealed class WebSocketOmpTransportStub : IAcpTransport
{
    private const string NotAvailableMessage =
        "WebSocket transport not available (stub): remote WebSocket transport: NotAvailable — owner: platform, tracked in docs/open-questions.md Q-HARN-WS";

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken) => FaultNotAvailableAsync();

    /// <inheritdoc />
    public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken) => FaultNotAvailableAsync();

    /// <inheritdoc />
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await FaultNotAvailableAsync().ConfigureAwait(false);
        yield break;
    }

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken) => FaultNotAvailableAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static Task FaultNotAvailableAsync() =>
        Task.FromException(new ProcessStartHarnessException(NotAvailableMessage, "WebSocket", new NotImplementedException(NotAvailableMessage)));
}
