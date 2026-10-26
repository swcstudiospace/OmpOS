using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.InMemory.Tests;

/// <summary>
/// Contract of <see cref="WebSocketOmpTransportStub"/>. Connect, send and receive each fault with exactly one
/// <see cref="ProcessStartHarnessException"/> (the typed NotAvailable failure, whose inner exception is a
/// <see cref="NotImplementedException"/>) and no other exception. Close completes without fault.
/// </summary>
public sealed class WebSocketOmpTransportStubContractTests
{
    /// <summary>Connect returns a task that faults with only the typed NotAvailable exception.</summary>
    [Fact]
    public async Task ConnectFaultsWithOnlyTheTypedNotAvailableException()
    {
        var ct = TestContext.Current.CancellationToken;
        var stub = new WebSocketOmpTransportStub();
        try
        {
            await AssertNotAvailableFaultAsync(stub.ConnectAsync(ct)).ConfigureAwait(true);
        }
        finally
        {
            await stub.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Send returns a task that faults with only the typed NotAvailable exception.</summary>
    [Fact]
    public async Task SendFaultsWithOnlyTheTypedNotAvailableException()
    {
        var ct = TestContext.Current.CancellationToken;
        var stub = new WebSocketOmpTransportStub();
        try
        {
            await AssertNotAvailableFaultAsync(stub.SendAsync(new byte[] { 1 }, ct)).ConfigureAwait(true);
        }
        finally
        {
            await stub.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Receive faults on its first step with only the typed NotAvailable exception.</summary>
    [Fact]
    public async Task ReceiveFaultsWithOnlyTheTypedNotAvailableException()
    {
        var ct = TestContext.Current.CancellationToken;
        var stub = new WebSocketOmpTransportStub();
        try
        {
            var frames = stub.ReceiveAsync(ct).GetAsyncEnumerator(ct);

            await AssertNotAvailableFaultAsync(frames.MoveNextAsync().AsTask()).ConfigureAwait(true);
            await frames.DisposeAsync().ConfigureAwait(true);
        }
        finally
        {
            await stub.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Close completes without fault, because an unconnected stub has nothing to close.</summary>
    [Fact]
    public async Task CloseCompletesWithoutFault()
    {
        var ct = TestContext.Current.CancellationToken;
        var stub = new WebSocketOmpTransportStub();
        try
        {
            var close = stub.CloseAsync(ct);
            await close.ConfigureAwait(true);

            close.IsCompletedSuccessfully.ShouldBeTrue();
        }
        finally
        {
            await stub.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static async Task AssertNotAvailableFaultAsync(Task operation)
    {
        var faulted = await operation.ContinueWith(static task => task.Exception, TaskScheduler.Default).ConfigureAwait(true);
        var aggregate = faulted.ShouldNotBeNull("the operation faults with an AggregateException");
        aggregate.InnerExceptions.Count.ShouldBe(1, "the operation faults with exactly one exception");
        var inner = aggregate.InnerExceptions[0];
        inner.GetType().ShouldBe(typeof(ProcessStartHarnessException));
        var thrown = (ProcessStartHarnessException)inner;
        thrown.Message.ShouldContain("NotAvailable");
        thrown.InnerException.ShouldBeOfType<NotImplementedException>();
    }
}
