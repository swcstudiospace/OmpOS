using System.Runtime.CompilerServices;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Engine;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>
/// A send path whose first turn streams one chunk and then ends only when the test releases it, so a test can steer while the turn runs and
/// let it end on its own, without a stop. Every later turn streams one chunk and ends at once. The channel records the prompts it received.
/// </summary>
internal sealed class GatedPromptChannel : IPromptChannel
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> _prompts = [];

    /// <summary>Gets the text of every prompt submitted, in order.</summary>
    public IReadOnlyList<string> Prompts => _prompts.ToArray();

    /// <summary>Lets the first turn end.</summary>
    public void ReleaseFirstTurn() => _release.TrySetResult();

    /// <inheritdoc />
    public async IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _prompts.Add(prompt);
        var first = _prompts.Count == 1;
        yield return new TextDeltaEvent(first ? "working" : "done");
        if (first)
        {
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        yield return new CompletedEvent("end_turn");
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken)
    {
        _release.TrySetResult();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
