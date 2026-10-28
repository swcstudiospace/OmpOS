using System.Collections.Concurrent;
using Autonogrammer.Harness.AgentLoop.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>A handler whose behaviour a test supplies. It records every request it receives, in arrival order.</summary>
internal sealed class FakeBridgeHandler : IHookBridgeHandler
{
    private readonly Func<BridgeRequest, CancellationToken, ValueTask<BridgeDecision>> _behaviour;
    private readonly ConcurrentQueue<BridgeRequest> _requests = new();
    private int _calls;

    /// <summary>Creates a handler.</summary>
    /// <param name="behaviour">Runs for every request and returns or throws what the test wants the server to see.</param>
    public FakeBridgeHandler(Func<BridgeRequest, CancellationToken, ValueTask<BridgeDecision>> behaviour) => _behaviour = behaviour;

    /// <summary>Gets the number of requests the handler has received.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Gets the requests the handler has received so far.</summary>
    public IReadOnlyList<BridgeRequest> Requests => [.. _requests];

    /// <summary>Creates a handler that answers every request with one decision.</summary>
    /// <param name="decision">The decision.</param>
    /// <returns>The handler.</returns>
    public static FakeBridgeHandler Returning(BridgeDecision decision) => new((_, _) => ValueTask.FromResult(decision));

    /// <summary>Creates a handler that throws for every request.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The handler.</returns>
    public static FakeBridgeHandler Throwing(Exception exception) => new((_, _) => throw exception);

    /// <inheritdoc />
    public ValueTask<BridgeDecision> HandleAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        _requests.Enqueue(request);
        return _behaviour(request, cancellationToken);
    }
}
