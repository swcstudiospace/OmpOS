using System.Runtime.CompilerServices;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Engine;

namespace OmpOS.Native.Tests.RunControls;

/// <summary>
/// The send path of a run the test controls turn by turn. Every submitted prompt starts a turn that streams nothing and ends only when
/// the test calls <see cref="ReleaseTurn"/>, or when the client cancels the turn in flight. The channel records the prompts it received in order.
/// </summary>
internal sealed class GatedPromptChannel : IPromptChannel
{
    private readonly Lock _sync = new();
    private readonly List<string> _prompts = [];
    private readonly SemaphoreSlim _gate = new(0);

    /// <summary>Gets the text of every prompt submitted, in order.</summary>
    public IReadOnlyList<string> Prompts
    {
        get
        {
            lock (_sync)
            {
                return [.. _prompts];
            }
        }
    }

    /// <summary>Ends the oldest turn that has not ended yet.</summary>
    public void ReleaseTurn() => _gate.Release();

    /// <inheritdoc/>
    public async IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _prompts.Add(prompt);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(true);
        yield return new CompletedEvent("end_turn");
    }

    /// <inheritdoc/>
    public Task CancelAsync(CancellationToken cancellationToken)
    {
        _gate.Release();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
