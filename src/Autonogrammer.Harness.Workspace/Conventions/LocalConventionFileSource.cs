using System.Globalization;
using System.Text;

namespace Autonogrammer.Workspace.Conventions;

/// <summary>
/// An <see cref="IConventionFileSource"/> over the local disk. It reads only inside the roots it was given: a path outside every root, or a path that
/// resolves through a symbolic link outside the root that holds it, is refused with <see cref="ConventionDiagnosticKind.EscapesRoot"/> and is never read.
/// </summary>
public sealed class LocalConventionFileSource : IConventionFileSource
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly List<AllowedRoot> _roots = [];

    /// <summary>Creates a source limited to the given roots. A root that does not exist or cannot be opened allows nothing.</summary>
    /// <param name="roots">The directories reads may stay inside.</param>
    /// <exception cref="ArgumentNullException"><paramref name="roots"/> is null.</exception>
    public LocalConventionFileSource(IEnumerable<string> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var full = Path.GetFullPath(root);
            if (!Directory.Exists(full))
            {
                continue;
            }

            try
            {
                _roots.Add(new AllowedRoot(TrimSeparators(full), new WorkspaceRoot(full)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A root that cannot be opened allows nothing.
            }
        }
    }

    /// <inheritdoc/>
    public Task<ConventionListResult> ListAsync(string directory, int maxEntries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(List(directory, maxEntries));
    }

    /// <inheritdoc/>
    public async Task<ConventionReadResult> ReadTextAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        var real = Guard(path, out var refusal);
        if (real is null)
        {
            return ConventionReadResult.Fail(ConventionDiagnosticKind.EscapesRoot, refusal);
        }

        try
        {
            var info = new FileInfo(real);
            if (!info.Exists)
            {
                return ConventionReadResult.Fail(ConventionDiagnosticKind.Unreadable, "the file does not exist");
            }

            if (info.Length > maxBytes)
            {
                return ConventionReadResult.Fail(
                    ConventionDiagnosticKind.TooLarge,
                    string.Create(CultureInfo.InvariantCulture, $"the file is larger than the {maxBytes / 1024} KiB limit"));
            }

            var bytes = await File.ReadAllBytesAsync(real, cancellationToken).ConfigureAwait(false);
            return Decode(bytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ConventionReadResult.Fail(ConventionDiagnosticKind.Unreadable, $"the file cannot be read: {exception.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        cancellationToken.ThrowIfCancellationRequested();
        var real = Guard(path, out _);
        return Task.FromResult(real is not null && (File.Exists(real) || Directory.Exists(real)));
    }

    private static ConventionReadResult Decode(byte[] bytes)
    {
        var span = bytes.AsSpan();
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            span = span[3..];
        }

        try
        {
            return ConventionReadResult.Read(StrictUtf8.GetString(span));
        }
        catch (DecoderFallbackException)
        {
            return ConventionReadResult.Fail(ConventionDiagnosticKind.Malformed, "the file is not valid UTF-8 text");
        }
    }

    private static string TrimSeparators(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        return path.Length > root.Length ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;
    }

    private ConventionListResult List(string directory, int maxEntries)
    {
        var real = Guard(directory, out var refusal);
        if (real is null)
        {
            return ConventionListResult.Fail(ConventionDiagnosticKind.EscapesRoot, refusal);
        }

        if (!Directory.Exists(real))
        {
            return ConventionListResult.Empty;
        }

        try
        {
            var entries = new List<ConventionEntry>();
            var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false, ReturnSpecialDirectories = false, AttributesToSkip = FileAttributes.None };
            foreach (var path in Directory.EnumerateFileSystemEntries(real, "*", options))
            {
                var info = new FileInfo(path);
                entries.Add(new ConventionEntry(Path.GetFileName(path), Directory.Exists(path), info.LinkTarget is not null));
            }

            entries.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
            return new ConventionListResult(entries.Count > maxEntries ? entries.GetRange(0, maxEntries) : entries);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ConventionListResult.Fail(ConventionDiagnosticKind.Unreadable, $"the folder cannot be read: {exception.Message}");
        }
    }

    /// <summary>Resolves a path to its real path, or returns null with the reason the path was refused.</summary>
    private string? Guard(string path, out string refusal)
    {
        refusal = "the path is outside the allowed roots";
        string full;
        try
        {
            full = TrimSeparators(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            refusal = "the path is not valid";
            return null;
        }

        foreach (var root in _roots)
        {
            if (string.Equals(full, root.Path, PathComparison))
            {
                return root.Root.FullPath;
            }

            var prefix = root.Path[^1] == Path.DirectorySeparatorChar ? root.Path : root.Path + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, PathComparison))
            {
                continue;
            }

            try
            {
                return root.Root.Resolve(full[prefix.Length..]);
            }
            catch (WorkspacePathException exception)
            {
                refusal = $"it is refused: {exception.Reason}";
                return null;
            }
        }

        return null;
    }

    private sealed record AllowedRoot(string Path, WorkspaceRoot Root);
}
