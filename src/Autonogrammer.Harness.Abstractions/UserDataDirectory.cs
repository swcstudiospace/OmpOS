using System.Diagnostics;
using System.Globalization;

namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// The one place that names the per-user data directory and moves the directory the previous product name used. Every site that keeps
/// user state (the MCP state, the environment profiles, the setup consent, the workspace sets, the agent loop state and the head services'
/// configuration) computes its directory with <see cref="PathFor"/>, a pure computation, and a composition root calls
/// <see cref="EnsureCurrent"/> once at startup, which is the only member that touches the file system.
/// </summary>
/// <remarks>
/// Migration applies to default roots only: a root the user configured explicitly is used as given and never migrated. A legacy directory is
/// moved once and atomically (a rename; copy then delete when the rename cannot cross volumes), a marker file records it, a file in the new
/// directory is never overwritten, and the legacy directory is never read afterwards. When both directories exist the new one wins, the legacy
/// one is left untouched and a warning is returned. A legacy directory that cannot be read is left alone with a warning; startup never fails
/// for it. Two processes that start together perform one migration: the check-then-move runs under an exclusive lock file beside the
/// directories, and the atomic rename is the final arbiter.
/// </remarks>
public static class UserDataDirectory
{
    /// <summary>The name of the per-user data directory.</summary>
    public const string CurrentName = "Autonogrammer";

    /// <summary>The name of the marker file written inside the new directory when the legacy directory was moved into it.</summary>
    public const string MarkerFileName = "migration.json";

    /// <summary>The name of the lock file that serializes the move. It sits beside the directories and is removed when the move ends.</summary>
    public const string LockFileName = ".autonogrammer-migrate.lock";

    private const int LockAttempts = 200;
    private const int LockRetryMilliseconds = 25;

    /// <summary>Returns the per-user data directory under a parent root. A pure computation: nothing is read or created.</summary>
    /// <param name="parentRoot">The parent root, such as the application data folder or <c>~/.local/share</c>.</param>
    /// <returns>The directory.</returns>
    /// <exception cref="ArgumentException"><paramref name="parentRoot"/> is null, empty or white space.</exception>
    public static string PathFor(string parentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentRoot);
        return Path.Combine(parentRoot, CurrentName);
    }

    /// <summary>Returns the directory the previous product name kept its state in under a parent root. A pure computation.</summary>
    /// <param name="parentRoot">The parent root.</param>
    /// <returns>The legacy directory.</returns>
    /// <exception cref="ArgumentException"><paramref name="parentRoot"/> is null, empty or white space.</exception>
    public static string LegacyPath(string parentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentRoot);
        return Path.Combine(parentRoot, LegacyBrandNames.UserDataDirectory);
    }

    /// <summary>
    /// Moves the legacy directory under a parent root to the current one when only the legacy one exists, and reports what happened. Call it
    /// once from a composition root before the stores are created; it is cheap when there is nothing to move.
    /// </summary>
    /// <param name="parentRoot">The parent root. It is a default root, never one the user configured.</param>
    /// <param name="warn">Receives each warning as it is raised, so a head can send it to its logger. Null sends it to <see cref="Trace"/>; it is never written to the console.</param>
    /// <param name="timeProvider">Stamps the marker file. The system clock when null.</param>
    /// <param name="enumerateEntries">Lists a directory to prove it is readable before it is moved. Null uses the file system; a test replaces it to fail the listing.</param>
    /// <returns>The current directory, whether this call moved the legacy one, and a warning or null.</returns>
    /// <exception cref="ArgumentException"><paramref name="parentRoot"/> is null, empty or white space.</exception>
    public static UserDataMigration EnsureCurrent(
        string parentRoot,
        Action<string>? warn = null,
        TimeProvider? timeProvider = null,
        Func<string, IEnumerable<string>>? enumerateEntries = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentRoot);
        var current = PathFor(parentRoot);
        var legacy = LegacyPath(parentRoot);
        if (!Directory.Exists(legacy))
        {
            return new UserDataMigration(current, false, null);
        }

        // Both directories are decided under the lock, never here: a starter that passed the check above may have raced a finished move, and that
        // move must not be reported as both present.
        return MigrateUnderLock(parentRoot, current, legacy, warn, timeProvider ?? TimeProvider.System, enumerateEntries ?? DefaultEnumerate);
    }

    private static UserDataMigration MigrateUnderLock(
        string parentRoot,
        string current,
        string legacy,
        Action<string>? warn,
        TimeProvider timeProvider,
        Func<string, IEnumerable<string>> enumerateEntries)
    {
        using var gate = AcquireLock(Path.Combine(parentRoot, LockFileName), out var lockProblem);
        if (gate is null)
        {
            return Warned(current, lockProblem ?? LockBusy(legacy), warn);
        }

        // Decide again now that nobody else can be between the check and the move: another starter may have finished while this one waited.
        if (!Directory.Exists(legacy))
        {
            return new UserDataMigration(current, false, null);
        }

        if (Directory.Exists(current))
        {
            return Warned(current, BothPresent(current, legacy), warn);
        }

        if (!IsReadable(legacy, enumerateEntries))
        {
            return Warned(current, Unreadable(legacy), warn);
        }

        return Move(parentRoot, current, legacy, warn, timeProvider);
    }

    private static UserDataMigration Move(string parentRoot, string current, string legacy, Action<string>? warn, TimeProvider timeProvider)
    {
        var problem = TryRename(legacy, current);
        if (problem is null)
        {
            return Finished(current, null, warn, timeProvider);
        }

        if (Directory.Exists(current))
        {
            // Something created the new directory between the check and the move. The atomic rename refused, so nothing was overwritten.
            return Warned(current, BothPresent(current, legacy), warn);
        }

        if (problem is UnauthorizedAccessException)
        {
            return Warned(current, Unreadable(legacy), warn);
        }

        // The rename could not be done in one step (another volume). Copy into a staging directory beside the target, publish it with one rename, then delete the source.
        var staging = Path.Combine(parentRoot, CurrentName + ".migrating-" + Guid.NewGuid().ToString("N"));
        if (!TryCopyTree(legacy, staging))
        {
            TryDelete(staging);
            return Warned(current, CopyFailed(legacy), warn);
        }

        if (TryRename(staging, current) is not null)
        {
            TryDelete(staging);
            return Directory.Exists(current)
                ? Warned(current, BothPresent(current, legacy), warn)
                : Warned(current, CopyFailed(legacy), warn);
        }

        var leftover = TryDelete(legacy) ? null : LegacyNotRemoved(legacy);
        return Finished(current, leftover, warn, timeProvider);
    }

    private static UserDataMigration Finished(string current, string? warning, Action<string>? warn, TimeProvider timeProvider)
    {
        var markerProblem = TryWriteMarker(current, timeProvider);
        var combined = warning is null ? markerProblem : markerProblem is null ? warning : warning + " " + markerProblem;
        if (combined is not null)
        {
            Report(combined, warn);
        }

        return new UserDataMigration(current, true, combined);
    }

    private static UserDataMigration Warned(string current, string warning, Action<string>? warn)
    {
        Report(warning, warn);
        return new UserDataMigration(current, false, warning);
    }

    private static void Report(string warning, Action<string>? warn)
    {
        if (warn is null)
        {
            Trace.TraceWarning(warning);
        }
        else
        {
            warn(warning);
        }
    }

    private static FileStream? AcquireLock(string lockPath, out string? problem)
    {
        problem = null;
        for (var attempt = 0; attempt < LockAttempts; attempt++)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                // Another starter holds the lock. Wait for it; it either finishes the move or fails, and this starter then decides again.
                Thread.Sleep(LockRetryMilliseconds);
            }
            catch (UnauthorizedAccessException)
            {
                problem = "The " + CurrentName + " data directory could not be prepared because its parent directory is not writable. The previous directory is left as it is.";
                return null;
            }
        }

        return null;
    }

    private static bool IsReadable(string directory, Func<string, IEnumerable<string>> enumerateEntries)
    {
        try
        {
            using var entries = enumerateEntries(directory).GetEnumerator();
            _ = entries.MoveNext();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static IEnumerable<string> DefaultEnumerate(string directory) => Directory.EnumerateFileSystemEntries(directory);

    private static Exception? TryRename(string from, string to)
    {
        try
        {
            Directory.Move(from, to);
            return null;
        }
        catch (IOException exception)
        {
            return exception;
        }
        catch (UnauthorizedAccessException exception)
        {
            return exception;
        }
    }

    private static bool TryCopyTree(string source, string destination)
    {
        try
        {
            _ = Directory.CreateDirectory(destination);
            var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.None, IgnoreInaccessible = false };
            foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos("*", options))
            {
                var target = Path.Combine(destination, Path.GetRelativePath(source, entry.FullName));
                if (entry.LinkTarget is not null)
                {
                    // A link is not copied: it would point somewhere the copy cannot vouch for. The move is refused and the legacy directory stays as it is.
                    return false;
                }

                if (entry is DirectoryInfo)
                {
                    _ = Directory.CreateDirectory(target);
                }
                else
                {
                    File.Copy(entry.FullName, target, overwrite: false);
                }
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? TryWriteMarker(string current, TimeProvider timeProvider)
    {
        var text = "{ \"migratedUtc\": \"" + timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture) + "\" }" + "\n";
        try
        {
            using var stream = new FileStream(Path.Combine(current, MarkerFileName), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream);
            writer.Write(text);
            return null;
        }
        catch (IOException)
        {
            // A marker that already exists is kept: nothing is overwritten. Any other failure leaves the directory in use without its marker.
            return File.Exists(Path.Combine(current, MarkerFileName)) ? null : "The " + CurrentName + " data directory was moved, but its migration marker could not be written.";
        }
        catch (UnauthorizedAccessException)
        {
            return "The " + CurrentName + " data directory was moved, but its migration marker could not be written.";
        }
    }

    private static string BothPresent(string current, string legacy) =>
        "Both the " + Path.GetFileName(current) + " and the " + Path.GetFileName(legacy) + " data directories exist under " + Path.GetDirectoryName(current) +
        ". The " + Path.GetFileName(current) + " directory is used and the " + Path.GetFileName(legacy) + " directory is left untouched.";

    private static string Unreadable(string legacy) =>
        "The " + Path.GetFileName(legacy) + " data directory under " + Path.GetDirectoryName(legacy) + " cannot be read, so it is left alone and the " + CurrentName + " directory is used.";

    private static string CopyFailed(string legacy) =>
        "The " + Path.GetFileName(legacy) + " data directory under " + Path.GetDirectoryName(legacy) + " could not be moved, so it is left alone and the " + CurrentName + " directory is used.";

    private static string LegacyNotRemoved(string legacy) =>
        "The " + Path.GetFileName(legacy) + " data directory under " + Path.GetDirectoryName(legacy) + " was copied to the " + CurrentName + " directory but could not be removed; the " + CurrentName + " directory is used.";

    private static string LockBusy(string legacy) =>
        "The " + Path.GetFileName(legacy) + " data directory under " + Path.GetDirectoryName(legacy) + " is being moved by another process, so it is left alone and the " + CurrentName + " directory is used.";
}
