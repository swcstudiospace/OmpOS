using System.Text;

namespace OmpOS.Api.Services;

/// <summary>
/// Applies an accept or a reject to the workspace folder. It writes only when the file on disk is in the state the decision expects (the text
/// before the edit for an accept, the text after it for a reject), so a user's newer change is never overwritten, and it writes only inside the
/// workspace folder: a reported path that resolves elsewhere, or through a link, is refused before any file is touched.
/// </summary>
internal static class WorkspaceChangeApplier
{
    private const int MaxPathLength = 4096;

    /// <summary>Applies a decision.</summary>
    /// <param name="root">The absolute workspace folder, or null when the API has none.</param>
    /// <param name="change">The change.</param>
    /// <param name="accept">True to accept (write the edited text), false to reject (restore the text before the edit).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What happened.</returns>
    public static async Task<ApplyOutcome> ApplyAsync(string? root, ReportedChange change, bool accept, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (root is null)
        {
            return new ApplyOutcome(ApplyKind.Recorded, "The API has no workspace folder, so the decision is recorded and no file was written.");
        }

        if (!TryResolve(root, change.Path, out var full))
        {
            return new ApplyOutcome(ApplyKind.Outside, "The reported path is outside the workspace folder, so nothing was written.");
        }

        try
        {
            var current = File.Exists(full) ? await File.ReadAllTextAsync(full, cancellationToken).ConfigureAwait(false) : null;
            var atBefore = string.Equals(current, change.OldText, StringComparison.Ordinal);
            var atAfter = string.Equals(current, change.NewText, StringComparison.Ordinal);
            if (accept)
            {
                if (atAfter)
                {
                    return new ApplyOutcome(ApplyKind.Applied, null);
                }

                if (!atBefore)
                {
                    return Conflict();
                }

                _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                await File.WriteAllTextAsync(full, change.NewText, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
                return new ApplyOutcome(ApplyKind.Applied, null);
            }

            if (atBefore)
            {
                return new ApplyOutcome(ApplyKind.Applied, null);
            }

            if (!atAfter)
            {
                return Conflict();
            }

            if (change.OldText is null)
            {
                File.Delete(full);
            }
            else
            {
                await File.WriteAllTextAsync(full, change.OldText, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            }

            return new ApplyOutcome(ApplyKind.Applied, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ApplyOutcome(ApplyKind.Failed, "The file could not be read or written.");
        }
    }

    /// <summary>Resolves a reported path inside the workspace folder.</summary>
    /// <param name="root">The absolute workspace folder without a trailing separator.</param>
    /// <param name="reported">The path as the harness reported it: relative to the folder, or absolute inside it.</param>
    /// <param name="full">The absolute path when resolved.</param>
    /// <returns>True when the path stays inside the folder and no component of it is a link.</returns>
    public static bool TryResolve(string root, string reported, out string full)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(reported);
        full = string.Empty;
        if (string.IsNullOrWhiteSpace(reported) || reported.Length > MaxPathLength || reported.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        var resolved = Path.GetFullPath(Path.IsPathRooted(reported) ? reported : Path.Combine(root, reported));
        var prefix = root + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var current = root;
        foreach (var segment in Path.GetRelativePath(root, resolved).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.Exists && info.LinkTarget is not null)
            {
                return false;
            }
        }

        full = resolved;
        return true;
    }

    private static ApplyOutcome Conflict() =>
        new(ApplyKind.Conflict, "The file in the workspace changed since the edit, so nothing was written.");
}
