using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace OmpOS.Harness.AgentLoop.Gsd;

/// <summary>
/// Kills a process and every process it started. The descendants are listed before anything is killed, because once a parent
/// exits its children are re-parented and can no longer be traced back to it. On Linux the tree is walked through
/// <c>/proc</c>; elsewhere the runtime's own tree kill is used.
/// </summary>
internal static class GsdProcessTree
{
    private const string ProcRoot = "/proc";

    /// <summary>Kills <paramref name="root"/> and all of its descendants. Processes that already exited are skipped.</summary>
    /// <param name="root">The process the host started.</param>
    public static void Kill(Process root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var descendants = OperatingSystem.IsLinux() ? DescendantIds(root.Id) : [];

        try
        {
            if (!root.HasExited)
            {
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
            foreach (var child in ChildrenOf(pending.Dequeue()))
            {
                descendants.Add(child);
                pending.Enqueue(child);
            }
        }

        return descendants;
    }

    private static List<int> ChildrenOf(int pid)
    {
        var children = new List<int>();
        var tasks = Path.Combine(ProcRoot, pid.ToString(CultureInfo.InvariantCulture), "task");
        if (!Directory.Exists(tasks))
        {
            return children;
        }

        // Kernels built with CONFIG_PROC_CHILDREN list each thread's children. Without it, the parents are scanned.
        var childrenFileSeen = false;
        foreach (var task in Directory.EnumerateDirectories(tasks))
        {
            var file = Path.Combine(task, "children");
            if (!File.Exists(file))
            {
                continue;
            }

            childrenFileSeen = true;
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException)
            {
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

        return childrenFileSeen ? children : ChildrenFromParentScan(pid);
    }

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
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        // The command name is in parentheses and may contain spaces, so the fields are read after the last ')'.
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
            // The process could not be signalled; it is not owned by this host or has already gone.
        }
    }
}
