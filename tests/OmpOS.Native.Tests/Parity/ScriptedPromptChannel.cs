using System.Runtime.CompilerServices;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Engine;

namespace OmpOS.Native.Tests.Parity;

/// <summary>
/// The send path of a shell under test. Each submitted prompt plays the next scripted turn, in order. A turn that holds ends only when the
/// client cancels it. The channel records the prompts it received and the cancellations it was asked for.
/// </summary>
internal sealed class ScriptedPromptChannel(params ScriptedTurn[] turns) : IPromptChannel
{
    private readonly Queue<ScriptedTurn> _turns = new(turns);
    private readonly List<string> _prompts = [];
    private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<PromptOptions> _options = [];
    private int _cancelCount;

    /// <summary>Gets the text of every prompt submitted, in order.</summary>
    public IReadOnlyList<string> Prompts => _prompts.ToArray();

    /// <summary>Gets how many times the client asked the channel to cancel the turn in flight.</summary>
    public int CancelCount => _cancelCount;

    /// <summary>Gets the options (the mentions that travel as resources) of every prompt submitted with options, in order.</summary>
    public IReadOnlyList<PromptOptions> Options => _options.ToArray();

    /// <inheritdoc />
    public async IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _prompts.Add(prompt);
        var turn = _turns.Dequeue();
        if (turn.Fault is not null)
        {
            throw new InvalidOperationException(turn.Fault);
        }

        foreach (var evt in turn.Events)
        {
            yield return evt;
        }

        if (turn.HoldUntilCancelled)
        {
            await _cancelled.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            yield return new CompletedEvent("cancelled");
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, PromptOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options.Add(options);
        return SubmitAsync(prompt, cancellationToken);
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken)
    {
        _cancelCount++;
        _cancelled.TrySetResult();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
