namespace Autonogrammer.AgentWorker.Bridge;

/// <summary>
/// Deletes one account's directory under the worker's workspace root, the directory the worker's sessions run in. It refuses rather
/// than guesses: a root that is not absolute, a target not strictly under the root, and a target equal to the root all throw before
/// anything is deleted. A link or junction is removed as an entry and never followed. A directory with an attached session is left alone.
/// </summary>
internal static class AccountPurge
{
    /// <summary>Deletes the account directory <c>root/ownerDir</c> unless one of its sessions is attached.</summary>
    /// <param name="workspaceRoot">The configured workspace root (<c>Worker:WorkspaceRoot</c>), an absolute path.</param>
    /// <param name="ownerDir">The owner directory name: 64 lowercase hex characters.</param>
    /// <param name="host">The process host whose attached sessions are checked.</param>
    /// <param name="cancellationToken">Cancels the deletion; cancellation is observed between entries.</param>
    /// <returns>The outcome: deleted, not found, or refused because a session is attached.</returns>
    /// <exception cref="ArgumentException"><paramref name="ownerDir"/> is not a well-formed owner directory name.</exception>
    /// <exception cref="InvalidOperationException">The root is not absolute or the target is not strictly under it. Nothing is deleted.</exception>
    public static AccountPurgeOutcome Delete(string workspaceRoot, string ownerDir, OmpProcessHost host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(host);
        if (!OwnerDirectory.IsValid(ownerDir))
        {
            throw new ArgumentException("The owner directory must be 64 lowercase hex characters.", nameof(ownerDir));
        }

        if (!Path.IsPathFullyQualified(workspaceRoot))
        {
            throw new InvalidOperationException("The workspace root is not an absolute path. Nothing was deleted.");
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot));
        var target = Path.GetFullPath(Path.Combine(root, ownerDir));
        if (!IsStrictlyUnder(target, root))
        {
            throw new InvalidOperationException("The account directory is not strictly under the workspace root. Nothing was deleted.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return host.RunExclusive(() =>
        {
            if (host.IsAnyAttachedUnder(target))
            {
                return AccountPurgeOutcome.Attached;
            }

            var directory = new DirectoryInfo(target);
            if (!directory.Exists && !IsLink(directory))
            {
                return AccountPurgeOutcome.NotFound;
            }

            Remove(directory, cancellationToken);
            return AccountPurgeOutcome.Deleted;
        });
    }

    private static void Remove(FileSystemInfo entry, CancellationToken cancellationToken)
    {
        if (!entry.Exists && entry.LinkTarget is null)
        {
            return;
        }

        if (entry is DirectoryInfo directory && !IsLink(directory))
        {
            DeleteTree(directory, cancellationToken);
            return;
        }

        DeleteEntry(entry);
    }

    private static void DeleteTree(DirectoryInfo directory, CancellationToken cancellationToken)
    {
        foreach (var entry in directory.GetFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            Remove(entry, cancellationToken);
        }

        directory.Delete(recursive: false);
    }

    /// <summary>Removes a file, or a link entry itself. A directory link is removed without touching its target.</summary>
    private static void DeleteEntry(FileSystemInfo entry)
    {
        if (entry is DirectoryInfo link && OperatingSystem.IsWindows())
        {
            link.Delete(recursive: false);
        }
        else
        {
            File.Delete(entry.FullName);
        }
    }

    private static bool IsStrictlyUnder(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return !string.Equals(path, root, comparison) && path.StartsWith(prefix, comparison);
    }

    private static bool IsLink(FileSystemInfo entry) =>
        entry.LinkTarget is not null || (entry.Exists && entry.Attributes.HasFlag(FileAttributes.ReparsePoint));
}
