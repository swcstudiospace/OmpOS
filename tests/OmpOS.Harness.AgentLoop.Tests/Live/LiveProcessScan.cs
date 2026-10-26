using System.Globalization;
using System.Text;

namespace OmpOS.Harness.AgentLoop.Tests.Live;

/// <summary>
/// Finds the processes of one live run through <c>/proc</c>. The run's isolated agent directory is unique to it and is
/// inherited by every process omp starts, so an environment entry naming it marks exactly the run's process tree.
/// </summary>
internal static class LiveProcessScan
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>Returns the processes whose environment holds an exact entry.</summary>
    /// <param name="marker">The <c>NAME=value</c> entry to look for.</param>
    /// <returns>The matching processes, never including this one.</returns>
    public static IReadOnlyList<LiveProcessInfo> FindByEnvironment(string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        var found = new List<LiveProcessInfo>();
        var self = Environment.ProcessId;
        foreach (var directory in Directory.EnumerateDirectories("/proc"))
        {
            var name = Path.GetFileName(directory);
            if (!int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid == self)
            {
                continue;
            }

            var environment = ReadEntries(Path.Combine(directory, "environ"));
            if (environment.Contains(marker, StringComparer.Ordinal))
            {
                found.Add(new LiveProcessInfo(pid, ReadEntries(Path.Combine(directory, "cmdline")), environment));
            }
        }

        return found;
    }

    /// <summary>Polls until no process carries the marker, or the timeout elapses.</summary>
    /// <param name="marker">The <c>NAME=value</c> entry to look for.</param>
    /// <param name="timeout">How long to wait for the tree to disappear.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The processes that were still present at the end; empty when the tree is gone.</returns>
    public static async Task<IReadOnlyList<LiveProcessInfo>> WaitForNoneAsync(string marker, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = TimeProvider.System.GetUtcNow() + timeout;
        var remaining = FindByEnvironment(marker);
        while (remaining.Count > 0 && TimeProvider.System.GetUtcNow() < deadline)
        {
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(true);
            remaining = FindByEnvironment(marker);
        }

        return remaining;
    }

    private static string[] ReadEntries(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            return Encoding.UTF8.GetString(bytes).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The process exited between listing and reading, or belongs to another user: it is not part of this run.
            return [];
        }
    }
}
