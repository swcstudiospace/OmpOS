namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// One hook process to run: what to start, where, with which environment and which standard input.
/// </summary>
/// <param name="Command">The shell string, or null for the <c>args</c> form.</param>
/// <param name="Args">The argument vector run without a shell, or null for the shell form.</param>
/// <param name="Shell">The shell the <paramref name="Command"/> runs in; <see cref="HookShell.None"/> for <paramref name="Args"/>.</param>
/// <param name="WorkingDirectory">The absolute directory the process starts in.</param>
/// <param name="Environment">
/// The complete environment of the child. The runner clears everything else, so a variable that is not listed here does
/// not reach the hook.
/// </param>
/// <param name="StandardInput">The JSON written to standard input before it is closed.</param>
/// <param name="MaxStandardOutputBytes">The most bytes of standard output that are kept; the rest is drained and reported as truncation.</param>
/// <param name="MaxStandardErrorBytes">The most bytes of standard error that are kept.</param>
public sealed record HookInvocation(
    string? Command,
    IReadOnlyList<string>? Args,
    HookShell Shell,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    ReadOnlyMemory<byte> StandardInput,
    int MaxStandardOutputBytes,
    int MaxStandardErrorBytes);
