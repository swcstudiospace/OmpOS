using System.Collections.Concurrent;

namespace OmpOS.Harness.Plugins.Tests;

/// <summary>
/// A runner that starts nothing. It records every command it is asked to run, so a test can count spawns and check their
/// order and arguments. Its behaviour is supplied per test.
/// </summary>
internal sealed class FakeHookProcessRunner : IHookProcessRunner
{
    private readonly ConcurrentQueue<HookCommand> _spawns = new();
    private readonly Func<HookCommand, CancellationToken, Task<HookProcessOutcome>> _behavior;

    /// <summary>
    /// Creates a runner. With no behaviour, every command exits with code 0 and prints nothing.
    /// </summary>
    /// <param name="behavior">The outcome for each command.</param>
    public FakeHookProcessRunner(Func<HookCommand, CancellationToken, Task<HookProcessOutcome>>? behavior = null)
    {
        _behavior = behavior ?? ((_, _) => Task.FromResult(new HookProcessOutcome(0, string.Empty, string.Empty)));
    }

    /// <summary>
    /// Every command the runner was asked to start, in order.
    /// </summary>
    public IReadOnlyList<HookCommand> Spawns => [.. _spawns];

    /// <summary>
    /// The number of processes that would have been spawned.
    /// </summary>
    public int SpawnCount => _spawns.Count;

    /// <inheritdoc />
    public Task<HookProcessOutcome> RunAsync(HookCommand command, int maxOutputBytes, CancellationToken cancellationToken)
    {
        _spawns.Enqueue(command);
        return _behavior(command, cancellationToken);
    }
}
