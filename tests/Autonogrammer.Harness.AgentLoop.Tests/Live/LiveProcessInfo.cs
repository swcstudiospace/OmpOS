namespace Autonogrammer.Harness.AgentLoop.Tests.Live;

/// <summary>
/// One process of a live run as <c>/proc</c> shows it.
/// </summary>
/// <param name="Pid">The process identifier.</param>
/// <param name="Arguments">The command line, one element per argument.</param>
/// <param name="EnvironmentEntries">The environment as <c>NAME=value</c> entries.</param>
internal sealed record LiveProcessInfo(int Pid, IReadOnlyList<string> Arguments, IReadOnlyList<string> EnvironmentEntries)
{
    /// <summary>Returns whether the command line holds the arguments as one consecutive run.</summary>
    /// <param name="sequence">The arguments to look for.</param>
    /// <returns>True when the sequence occurs.</returns>
    public bool HasArgumentRun(params string[] sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        for (var start = 0; start + sequence.Length <= Arguments.Count; start++)
        {
            var matches = true;
            for (var offset = 0; offset < sequence.Length && matches; offset++)
            {
                matches = string.Equals(Arguments[start + offset], sequence[offset], StringComparison.Ordinal);
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the environment holds an exact entry.</summary>
    /// <param name="entry">The <c>NAME=value</c> entry.</param>
    /// <returns>True when present.</returns>
    public bool HasEnvironmentEntry(string entry) => EnvironmentEntries.Contains(entry, StringComparer.Ordinal);
}
