using System.Text;
using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>A hook command runner that starts nothing: it records every invocation and answers from a script, so a test counts spawns and reads the input that would reach the process.</summary>
internal sealed class ScriptedHookRunner : IHookCommandRunner
{
    private readonly List<HookInvocation> _invocations = [];
    private readonly Lock _gate = new();

    /// <summary>Gets or sets the answer for each invocation. The default exits 0 and prints an allow.</summary>
    public Func<HookInvocation, CancellationToken, Task<HookCommandOutcome>> Behavior { get; set; } = static (_, _) => Task.FromResult(Say("{\"permission\":\"allow\"}"));

    /// <summary>Gets a snapshot of the invocations, in order.</summary>
    public IReadOnlyList<HookInvocation> Invocations
    {
        get
        {
            lock (_gate)
            {
                return [.. _invocations];
            }
        }
    }

    /// <summary>Gets the number of processes that would have been started.</summary>
    public int SpawnCount => Invocations.Count;

    /// <summary>Builds an outcome that exited with a code and printed the text.</summary>
    /// <param name="standardOutput">What the hook printed.</param>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>The outcome.</returns>
    public static HookCommandOutcome Say(string standardOutput, int exitCode = 0) => new(exitCode, standardOutput, string.Empty, false, false);

    /// <summary>Returns the text a hook received on standard input.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>The JSON text.</returns>
    public static string InputOf(HookInvocation invocation) => Encoding.UTF8.GetString(invocation.StandardInput.Span);

    /// <inheritdoc />
    public Task<HookCommandOutcome> RunAsync(HookInvocation invocation, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _invocations.Add(invocation);
        }

        return Behavior(invocation, cancellationToken);
    }
}
