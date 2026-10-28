using System.Collections.Concurrent;
using Autonogrammer.Harness.Plugins;

namespace Autonogrammer.Native.Tests.Hooks;

/// <summary>
/// A process runner that starts nothing. It records every command it is asked to start, so a test can count spawns and
/// check which commands ran. Every command reports exit code 0.
/// </summary>
internal sealed class CountingHookProcessRunner : IHookProcessRunner
{
    private readonly ConcurrentQueue<HookCommand> _spawns = new();

    /// <summary>Gets every command the runner was asked to start, in order.</summary>
    public IReadOnlyList<HookCommand> Spawns => [.. _spawns];

    /// <summary>Gets the number of processes that would have been started.</summary>
    public int SpawnCount => _spawns.Count;

    /// <inheritdoc />
    public Task<HookProcessOutcome> RunAsync(HookCommand command, int maxOutputBytes, CancellationToken cancellationToken)
    {
        _spawns.Enqueue(command);
        return Task.FromResult(new HookProcessOutcome(0, string.Empty, string.Empty));
    }
}
