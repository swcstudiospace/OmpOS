using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace OmpOS.Harness.Plugins;

/// <summary>
/// Kills a process and every process it started. The descendants are listed before anything is killed, because once a
/// parent exits its children are re-parented and can no longer be traced back to it. On Linux the tree is walked through
/// <c>/proc</c> from the root down, so the cost follows the size of the tree rather than the number of processes on the
/// machine.
/// </summary>
internal static class ProcessTree
{
    private const string ProcRoot = "/proc";

    // Kernels built with CONFIG_PROC_CHILDREN expose each thread's children. Without it, the parent of every process is
    // read from its stat file instead.
    private static readonly bool ChildrenFileSupported = OperatingSystem.IsLinux()
        && Directory.EnumerateDirectories(Path.Combine(ProcRoot, "self", "task")).Any(task => File.Exists(Path.Combine(task, "children")));

    /// <summary>
    /// Kills <paramref name="root"/> and all of its descendants. Processes that have already exited are skipped.
    /// </summary>
    /// <param name="root">The process that was started by the host.</param>
    public static void Kill(Process root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var descendants = OperatingSystem.IsLinux() ? DescendantIds(root.Id) : [];

        try
        {
            if (!root.HasExited)
            {
                // On Linux the descendants are killed from the list above, so only the root is signalled here.
                root.Kill(entireProcessTree: !OperatingSystem.IsLinux());
            }
        }
        catch (InvalidOperationException)
        {
            // The root exited between the check and the kill. Its descendants are still handled below.
        }

        foreach (var id in descendants)
        {
            KillById(id);
        }
    }

    private static List<int> DescendantIds(int rootId)
    {
        var descendants = new List<int>();
        var pending = new Queue<int>();
        pending.Enqueue(rootId);
        while (pending.Count > 0)
        {
            var parent = pending.Dequeue();
            foreach (var child in ChildrenOf(parent))
            {
                descendants.Add(child);
                pending.Enqueue(child);
            }
        }

        return descendants;
    }

    private static List<int> ChildrenOf(int pid)
    {
        if (ChildrenFileSupported)
        {
            return ChildrenFromTaskFiles(pid);
        }

        return ChildrenFromParentScan(pid);
    }

    // Every thread of the process can start children, so each thread's list is read.
    private static List<int> ChildrenFromTaskFiles(int pid)
    {
        var children = new List<int>();
        var tasks = Path.Combine(ProcRoot, pid.ToString(CultureInfo.InvariantCulture), "task");
        if (!Directory.Exists(tasks))
        {
            return children;
        }

        foreach (var task in Directory.EnumerateDirectories(tasks))
        {
            string text;
            try
            {
                text = File.ReadAllText(Path.Combine(task, "children"));
            }
            catch (IOException)
            {
                // The thread exited while its list was being read.
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var child))
                {
                    children.Add(child);
                }
            }
        }

        return children;
    }

    // The fallback reads every process's parent. It is slower on a busy machine, so it runs only where the task lists are absent.
    private static List<int> ChildrenFromParentScan(int pid)
    {
        var children = new List<int>();
        foreach (var directory in Directory.EnumerateDirectories(ProcRoot))
        {
            if (int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && TryReadParentId(directory, out var parentId)
                && parentId == pid)
            {
                children.Add(id);
            }
        }

        return children;
    }

    private static bool TryReadParentId(string processDirectory, out int parentId)
    {
        parentId = 0;
        string stat;
        try
        {
            stat = File.ReadAllText(Path.Combine(processDirectory, "stat"));
        }
        catch (IOException)
        {
            // The process exited while its entry was being read.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        // The command name is in parentheses and may contain spaces or parentheses, so the fields are read after the last ')'.
        // After it come the state and then the parent identifier.
        var close = stat.LastIndexOf(')');
        if (close < 0 || close + 2 >= stat.Length)
        {
            return false;
        }

        var fields = stat[(close + 2)..].Split(' ');
        return fields.Length > 1 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out parentId);
    }

    private static void KillById(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            process.Kill();
        }
        catch (ArgumentException)
        {
            // The process is no longer running.
        }
        catch (InvalidOperationException)
        {
            // The process exited between the lookup and the kill.
        }
        catch (Win32Exception)
        {
            // The process could not be signalled. It is not owned by this host or has already gone.
        }
    }
}
