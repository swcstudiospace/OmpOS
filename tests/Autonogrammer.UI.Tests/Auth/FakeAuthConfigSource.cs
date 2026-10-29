using Autonogrammer.UI.Components.Auth;

namespace Autonogrammer.UI.Tests.Auth;

/// <summary>An <see cref="IAuthConfigSource"/> that answers from a queue, or waits for the test to release it.</summary>
internal sealed class FakeAuthConfigSource : IAuthConfigSource
{
    private readonly Queue<TaskCompletionSource<AuthConfig?>> _answers = new();

    /// <summary>Gets how many times the configuration was loaded.</summary>
    internal int Loads { get; private set; }

    /// <summary>Queues an answer that is ready at once.</summary>
    /// <param name="config">The configuration, or null for an unreachable API.</param>
    internal void Enqueue(AuthConfig? config)
    {
        var answer = new TaskCompletionSource<AuthConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);
        answer.SetResult(config);
        _answers.Enqueue(answer);
    }

    /// <summary>Queues an answer the test completes later.</summary>
    /// <returns>The source of the answer.</returns>
    internal TaskCompletionSource<AuthConfig?> EnqueuePending()
    {
        var answer = new TaskCompletionSource<AuthConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _answers.Enqueue(answer);
        return answer;
    }

    /// <inheritdoc />
    public async Task<AuthConfig?> LoadAsync(CancellationToken cancellationToken)
    {
        Loads++;
        return await _answers.Dequeue().Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
