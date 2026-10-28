using System.Runtime.CompilerServices;
using OmpOS.Presentation.Terminal;

namespace OmpOS.Web.Demo;

/// <summary>
/// The Demo's <see cref="IRemoteTerminalTransport"/>: a scripted omp terminal session, labelled as such, delivered chunk by chunk on the
/// injected <see cref="TimeProvider"/>. Reconnecting with an acknowledged sequence resumes after it. After the last chunk the attachment stays open
/// until the view detaches, like a terminal that is waiting for input.
/// </summary>
internal sealed class DemoTerminalTransport : IRemoteTerminalTransport
{
    private static readonly string[] Script =
    [
        "\u001b[35momp terminal\u001b[0m (Demo: scripted output, no process is running)\r\n",
        "\r\n",
        "\u001b[2mworkspace\u001b[0m acme-shop\r\n",
        "\u001b[2mmodel\u001b[0m anthropic/claude-sonnet-4-5\r\n",
        "\r\n",
        "> list the failing tests\r\n",
        "\u001b[36mrunning\u001b[0m dotnet test --no-build\r\n",
        "  Passed: 211  Failed: 1  Skipped: 0\r\n",
        "  Failed: CheckoutTotalTests.RoundsHalfUpToTheCent\r\n",
        "\r\n",
        "The agent in the first session is fixing that one.\r\n",
        "> ",
    ];

    private readonly TimeProvider _time;
    private readonly TimeSpan _delay;

    /// <summary>Initializes a new instance of the <see cref="DemoTerminalTransport"/> class.</summary>
    /// <param name="time">The clock of the cadence.</param>
    /// <param name="delay">The pause between two chunks, or null for 150 ms.</param>
    internal DemoTerminalTransport(TimeProvider time, TimeSpan? delay = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        _delay = delay ?? TimeSpan.FromMilliseconds(150);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<RemoteTerminalChunk> AttachAsync(string terminalId, long afterSequence, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(terminalId);
        for (var index = 0; index < Script.Length; index++)
        {
            var sequence = index + 1L;
            if (sequence <= afterSequence)
            {
                continue;
            }

            await Task.Delay(_delay, _time, cancellationToken).ConfigureAwait(false);
            yield return new RemoteTerminalChunk(sequence, Script[index]);
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, _time, cancellationToken).ConfigureAwait(false);
    }
}
