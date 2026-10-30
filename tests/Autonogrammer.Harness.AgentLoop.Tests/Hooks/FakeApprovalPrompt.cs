using Autonogrammer.Harness.AgentLoop.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>An approval prompt whose answer a test supplies. It records what it was asked.</summary>
internal sealed class FakeApprovalPrompt : IHookApprovalPrompt
{
    private readonly Func<BridgeRequest, BridgeDecision, CancellationToken, ValueTask<bool>> _behaviour;
    private int _calls;

    private FakeApprovalPrompt(Func<BridgeRequest, BridgeDecision, CancellationToken, ValueTask<bool>> behaviour) => _behaviour = behaviour;

    /// <summary>Gets the number of times the prompt was asked.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Gets the request of the last question, or null when the prompt was never asked.</summary>
    public BridgeRequest? LastRequest { get; private set; }

    /// <summary>Gets the decision of the last question, or null when the prompt was never asked.</summary>
    public BridgeDecision? LastDecision { get; private set; }

    /// <summary>Creates a prompt that gives the same answer every time.</summary>
    /// <param name="approved">The answer.</param>
    /// <returns>The prompt.</returns>
    public static FakeApprovalPrompt Answering(bool approved) => new((_, _, _) => ValueTask.FromResult(approved));

    /// <summary>Creates a prompt that throws every time.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The prompt.</returns>
    public static FakeApprovalPrompt Throwing(Exception exception) => new((_, _, _) => throw exception);

    /// <summary>Creates a prompt with arbitrary behaviour.</summary>
    /// <param name="behaviour">Runs for every question.</param>
    /// <returns>The prompt.</returns>
    public static FakeApprovalPrompt Running(Func<BridgeRequest, BridgeDecision, CancellationToken, ValueTask<bool>> behaviour) => new(behaviour);

    /// <inheritdoc />
    public ValueTask<bool> AskAsync(BridgeRequest request, BridgeDecision decision, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        LastRequest = request;
        LastDecision = decision;
        return _behaviour(request, decision, cancellationToken);
    }
}
