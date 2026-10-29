using Autonogrammer.UI.Components.Auth;

namespace Autonogrammer.UI.Tests.Auth;

/// <summary>An <see cref="IOwnerTokenConnector"/> that answers from a queue, or waits for the test to release it.</summary>
internal sealed class FakeConnector : IOwnerTokenConnector
{
    private readonly Queue<TaskCompletionSource<ConnectOutcome>> _answers = new();

    /// <summary>Gets the tokens it was asked to try, in order.</summary>
    internal List<string> Tokens { get; } = [];

    /// <summary>Queues an answer that is ready at once.</summary>
    /// <param name="outcome">The outcome.</param>
    internal void Enqueue(ConnectOutcome outcome)
    {
        var answer = new TaskCompletionSource<ConnectOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        answer.SetResult(outcome);
        _answers.Enqueue(answer);
    }

    /// <summary>Queues an answer the test completes later.</summary>
    /// <returns>The source of the answer.</returns>
    internal TaskCompletionSource<ConnectOutcome> EnqueuePending()
    {
        var answer = new TaskCompletionSource<ConnectOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        _answers.Enqueue(answer);
        return answer;
    }

    /// <inheritdoc />
    public async Task<ConnectOutcome> ConnectAsync(string token, CancellationToken cancellationToken)
    {
        Tokens.Add(token);
        return await _answers.Dequeue().Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
