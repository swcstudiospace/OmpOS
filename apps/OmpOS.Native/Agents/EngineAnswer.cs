using System.Text;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Native.Agents;

/// <summary>Reads the answer of one turn of a child engine (a side chat, a best-of-n candidate): the assistant text, or the failure that ended the turn.</summary>
internal static class EngineAnswer
{
    /// <summary>Streams a turn to its end and returns the assistant text it produced.</summary>
    /// <param name="events">The events of the turn.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The assistant text, trimmed.</returns>
    /// <exception cref="InvalidOperationException">The turn failed, or ended without any assistant text.</exception>
    public static async Task<string> CollectAsync(IAsyncEnumerable<HarnessEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        var text = new StringBuilder();
        await foreach (var harnessEvent in events.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            switch (harnessEvent)
            {
                case TextDeltaEvent delta:
                    text.Append(delta.Text);
                    break;
                case ErrorEvent error:
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error.Message) ? "The agent failed." : error.Message);
                default:
                    break;
            }
        }

        var answer = text.ToString().Trim();
        return answer.Length > 0 ? answer : throw new InvalidOperationException("The agent ended without an answer.");
    }
}
