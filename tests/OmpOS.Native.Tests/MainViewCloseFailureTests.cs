using System.Diagnostics;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.Parity;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests;

/// <summary>
/// Native checks for closing the main view when its engine host fails to dispose. The host closes off the UI thread, and the window
/// is already closing, so a failure cannot reach the status line. The caller's continuation traces it instead.
/// </summary>
public sealed class MainViewCloseFailureTests
{
    /// <summary>How long the test waits for the close failure to reach the trace.</summary>
    private static readonly TimeSpan TraceTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Closing the window with an engine host whose dispose fails traces the failure, with the dispose error's message.</summary>
    [Fact]
    public async Task ClosingTheWindowTracesAnEngineHostDisposeFailure()
    {
        await using var session = TestAppBuilder.StartSession();
        var traced = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new CloseFailureListener(traced);
        Trace.Listeners.Add(listener);
        try
        {
            await session.Dispatch(() =>
            {
                var main = ParityShellHarness.CreateMain(new DisposeFailingChannel());
                ParityShellHarness.Show(main);
                main.Close();
            }, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var message = await traced.Task.WaitAsync(TraceTimeout, TestContext.Current.CancellationToken).ConfigureAwait(true);
            message.ShouldContain("engine dispose failed");
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    /// <summary>
    /// A send path that is never used and whose disposal fails. The close path cancels only a turn in flight and this channel never has
    /// one, so it refuses both the prompt and the cancel.
    /// </summary>
    private sealed class DisposeFailingChannel : IPromptChannel
    {
        /// <summary>Refuses every prompt: the shell is closed while idle, so none is submitted.</summary>
        /// <param name="prompt">The prompt text, which is never read.</param>
        /// <param name="cancellationToken">The cancellation token, which is never read.</param>
        /// <returns>Never returns; the call throws.</returns>
        public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This channel accepts no prompts.");

        /// <summary>Refuses every cancel: the channel never has a turn in flight.</summary>
        /// <param name="cancellationToken">The cancellation token, which is never read.</param>
        /// <returns>Never returns; the call throws.</returns>
        public Task CancelAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("This channel accepts no cancellations.");

        /// <summary>Fails the dispose the way a host whose teardown breaks does.</summary>
        /// <returns>A value task that has faulted with the dispose failure.</returns>
        public ValueTask DisposeAsync() => ValueTask.FromException(new InvalidOperationException("engine dispose failed"));
    }

    /// <summary>A trace listener that completes once a close failure is traced, with the traced text.</summary>
    /// <param name="traced">Completed with the first traced message that reports the failed close.</param>
    private sealed class CloseFailureListener(TaskCompletionSource<string> traced) : TraceListener
    {
        /// <summary>The fragment of the trace the main view's close continuation writes on a failed close.</summary>
        private const string Marker = "could not close the engine host";

        /// <inheritdoc />
        public override void Write(string? message) => Record(message);

        /// <inheritdoc />
        public override void WriteLine(string? message) => Record(message);

        /// <inheritdoc />
        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message) =>
            Record(message);

        /// <summary>Completes the trace task when the text reports a failed close.</summary>
        /// <param name="text">The text the listener received.</param>
        private void Record(string? text)
        {
            if (text is not null && text.Contains(Marker, StringComparison.Ordinal))
            {
                traced.TrySetResult(text);
            }
        }
    }
}
