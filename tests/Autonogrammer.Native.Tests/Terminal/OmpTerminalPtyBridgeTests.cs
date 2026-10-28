using System.Text;
using Autonogrammer.Native.Views.Terminal;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Terminal;

/// <summary>
/// The bridge between the terminal control and an omp session: output queued for the control arrives as bytes in order,
/// what the control writes reaches the attached session as text, a size reported by the control is remembered and applied,
/// and input with no session is dropped. The session is a fake, so no process starts.
/// </summary>
public sealed class OmpTerminalPtyBridgeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueuedOutputIsReadAsBytesInOrderAcrossSmallReads()
    {
        using var bridge = new OmpTerminalPtyBridge((_, _) => { });
        bridge.Write("héllo ");
        bridge.Write("wörld");
        var buffer = new byte[4];
        var collected = new List<byte>();
        var expected = Encoding.UTF8.GetBytes("héllo wörld");

        while (collected.Count < expected.Length)
        {
            var read = await bridge.ReaderStream.ReadAsync(buffer.AsMemory(), Token).ConfigureAwait(true);
            read.ShouldBeGreaterThan(0);
            collected.AddRange(buffer.AsSpan(0, read).ToArray());
        }

        Encoding.UTF8.GetString(collected.ToArray()).ShouldBe("héllo wörld");
    }

    [Fact]
    public async Task AReadWaitsForOutputAndACancelledReadEndsWithoutLosingLaterOutput()
    {
        using var bridge = new OmpTerminalPtyBridge((_, _) => { });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var buffer = new byte[16];

        var pending = bridge.ReaderStream.ReadAsync(buffer.AsMemory(), cancellation.Token).AsTask();
        pending.IsCompleted.ShouldBeFalse();
        await cancellation.CancelAsync().ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(pending).ConfigureAwait(true);

        bridge.Write("later");
        var read = await bridge.ReaderStream.ReadAsync(buffer.AsMemory(), Token).ConfigureAwait(true);

        Encoding.UTF8.GetString(buffer, 0, read).ShouldBe("later");
    }

    [Fact]
    public async Task DisposeEndsTheOutputStream()
    {
        var bridge = new OmpTerminalPtyBridge((_, _) => { });
        var other = new byte[8];
        bridge.Write("tail");
        var stream = bridge.ReaderStream;
        bridge.Dispose();

        var first = await ReadOnceAsync(stream, other).ConfigureAwait(true);
        first.ShouldBe(4);
        (await ReadOnceAsync(stream, other).ConfigureAwait(true)).ShouldBe(0);
    }

    [Fact]
    public async Task TextWrittenByTheControlReachesTheAttachedSession()
    {
        using var bridge = new OmpTerminalPtyBridge((_, _) => { });
        var session = new FakeTerminalSession();
        await using var sessionScope = session.ConfigureAwait(true);
        bridge.AttachSession(session);

        await bridge.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("ls é\r"), Token).ConfigureAwait(true);
        await bridge.WriterStream.FlushAsync(Token).ConfigureAwait(true);

        session.Writes.ShouldBe(["ls é\r"]);
    }

    [Fact]
    public async Task InputWithNoSessionIsDroppedAndDoesNotThrow()
    {
        using var bridge = new OmpTerminalPtyBridge((_, _) => { });

        await bridge.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("x"), Token).ConfigureAwait(true);

        bridge.Pid.ShouldBe(0);
    }

    [Fact]
    public async Task InputToASessionThatEndedIsDroppedWithoutAnException()
    {
        using var bridge = new OmpTerminalPtyBridge((_, _) => { });
        var throwing = new ThrowingSession(new ObjectDisposedException("session"));
        await using var throwingScope = throwing.ConfigureAwait(true);
        bridge.AttachSession(throwing);
        await bridge.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("x"), Token).ConfigureAwait(true);

        var closing = new ThrowingSession(new IOException("closed"));
        await using var closingScope = closing.ConfigureAwait(true);
        bridge.AttachSession(closing);
        await bridge.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("y"), Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task ASizeReportedByTheControlIsReportedToTheOwnerAndAppliedToTheSession()
    {
        var reported = new List<(int, int)>();
        using var bridge = new OmpTerminalPtyBridge((columns, rows) => reported.Add((columns, rows)));
        var session = new FakeTerminalSession();
        await using var sessionScope = session.ConfigureAwait(true);
        bridge.AttachSession(session);

        bridge.Resize(140, 50);

        reported.ShouldBe([(140, 50)]);
        session.Resizes.ShouldBe([(140, 50)]);
    }

    [Fact]
    public async Task ASizeReportedBeforeASessionIsAttachedIsGivenToTheSessionWhenItAttaches()
    {
        using var bridge = new OmpTerminalPtyBridge((_, _) => { });
        bridge.Resize(101, 33);
        var session = new FakeTerminalSession();
        await using var sessionScope = session.ConfigureAwait(true);

        bridge.AttachSession(session);

        session.Resizes.ShouldBe([(101, 33)]);
    }

    [Fact]
    public async Task ASizeBelowOneIsIgnored()
    {
        var reported = 0;
        using var bridge = new OmpTerminalPtyBridge((_, _) => reported++);
        var session = new FakeTerminalSession();
        await using var sessionScope = session.ConfigureAwait(true);
        bridge.AttachSession(session);

        bridge.Resize(0, 24);
        bridge.Resize(80, 0);

        reported.ShouldBe(0);
        session.Resizes.ShouldBeEmpty();
    }

    [Fact]
    public async Task AResizeOfADisposedSessionIsIgnored()
    {
        using var bridge = new OmpTerminalPtyBridge((_, _) => { });
        var throwing = new ThrowingSession(new ObjectDisposedException("session"));
        await using var throwingScope = throwing.ConfigureAwait(true);
        bridge.AttachSession(throwing);

        Should.NotThrow(() => bridge.Resize(80, 24));
    }

    [Fact]
    public void TheBridgeNeverReportsAnExit()
    {
        using var bridge = new OmpTerminalPtyBridge((_, _) => { });

        bridge.WaitForExit(1).ShouldBeFalse();
        bridge.ExitCode.ShouldBe(0);
        bridge.SupportsCancellableRead.ShouldBeTrue();
    }

    private static async Task<int> ReadOnceAsync(Stream stream, byte[] buffer) =>
        await stream.ReadAsync(buffer.AsMemory(), Token).ConfigureAwait(true);

    private sealed class ThrowingSession(Exception exception) : Autonogrammer.Harness.Terminal.ITerminalSession
    {
        public Task WriteInputAsync(string text, CancellationToken cancellationToken) => Task.FromException(exception);

        public void Resize(int columns, int rows)
        {
            ObjectDisposedException.ThrowIf(exception is ObjectDisposedException, "session");
        }

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
