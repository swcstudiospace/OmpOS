using System.Collections.Concurrent;
using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// A hook command runner that starts nothing. It records every invocation and answers from a script, so a test can count
/// spawns and check the input, environment and working directory that would reach the process.
/// </summary>
internal sealed class ScriptedCommandRunner : IHookCommandRunner
{
    private readonly ConcurrentQueue<HookInvocation> _invocations = new();
    private readonly Func<HookInvocation, CancellationToken, Task<HookCommandOutcome>> _behavior;

    /// <summary>
    /// Creates a runner. With no behaviour, every hook exits 0 and prints nothing.
    /// </summary>
    /// <param name="behavior">The outcome for each invocation.</param>
    public ScriptedCommandRunner(Func<HookInvocation, CancellationToken, Task<HookCommandOutcome>>? behavior = null)
    {
        _behavior = behavior ?? ((_, _) => Task.FromResult(Exit(0)));
    }

    /// <summary>Every invocation, in order.</summary>
    public IReadOnlyList<HookInvocation> Invocations => [.. _invocations];

    /// <summary>The number of processes that would have been spawned.</summary>
    public int SpawnCount => _invocations.Count;

    /// <summary>Creates an outcome that exited with a code and wrote the given text.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="standardOutput">What the hook printed to standard output.</param>
    /// <param name="standardError">What the hook printed to standard error.</param>
    public static HookCommandOutcome Exit(int exitCode, string standardOutput = "", string standardError = "") =>
        new(exitCode, standardOutput, standardError, false, false);

    /// <summary>Creates a runner that answers every invocation with the same outcome.</summary>
    /// <param name="outcome">The outcome.</param>
    public static ScriptedCommandRunner Always(HookCommandOutcome outcome) => new((_, _) => Task.FromResult(outcome));

    /// <inheritdoc />
    public Task<HookCommandOutcome> RunAsync(HookInvocation invocation, CancellationToken cancellationToken)
    {
        _invocations.Enqueue(invocation);
        return _behavior(invocation, cancellationToken);
    }
}
