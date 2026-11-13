using Autonogrammer.Api.Services;

namespace Autonogrammer.Api.Operations;

/// <summary>
/// Deletes one account's workspace folder during operator erasure. It refuses rather than guesses: a base that is not absolute, a folder that is not strictly
/// under the base, and a folder that is itself a symbolic link or junction all throw before anything is deleted. Links inside the folder are removed as entries
/// and never followed.
/// </summary>
internal static class AccountWorkspaceEraser
{
    /// <summary>Deletes the account's workspace folder under the base. Returns at once when no base is configured or the folder does not exist.</summary>
    /// <param name="baseRoot">The configured workspace base (<c>Api:Workspace:Root</c>), or null or empty when none is configured.</param>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancels the deletion; cancellation is observed between entries.</param>
    /// <returns>A task that completes when the folder is deleted or was already absent.</returns>
    /// <exception cref="InvalidOperationException">The base is not absolute, the folder is not strictly under the base, or the folder is a link. Nothing is deleted.</exception>
    public static Task DeleteAsync(string? baseRoot, string accountId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baseRoot))
        {
            return Task.CompletedTask;
        }

        return Task.Run(() => DeleteFolder(baseRoot, accountId, cancellationToken), cancellationToken);
    }

    private static void DeleteFolder(string baseRoot, string accountId, CancellationToken cancellationToken)
    {
        var folder = AccountWorkspace.Resolve(baseRoot, accountId)
            ?? throw new InvalidOperationException("The workspace base is not an absolute path. Nothing was deleted.");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseRoot));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (string.Equals(folder, root, comparison) || !folder.StartsWith(prefix, comparison))
        {
            throw new InvalidOperationException("The account workspace folder is not strictly under the workspace base. Nothing was deleted.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var directory = new DirectoryInfo(folder);
        if (IsLink(directory))
        {
            throw new InvalidOperationException("The account workspace folder is a symbolic link or junction. Nothing was deleted.");
        }

        if (directory.Exists)
        {
            DeleteTree(directory, cancellationToken);
        }
    }

    private static void DeleteTree(DirectoryInfo directory, CancellationToken cancellationToken)
    {
        foreach (var entry in directory.GetFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry is DirectoryInfo child && !IsLink(child))
            {
                DeleteTree(child, cancellationToken);
            }
            else
            {
                DeleteEntry(entry);
            }
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

    private static bool IsLink(FileSystemInfo entry) =>
        entry.LinkTarget is not null || (entry.Exists && entry.Attributes.HasFlag(FileAttributes.ReparsePoint));
}
