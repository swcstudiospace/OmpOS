using System.Text;
using System.Text.RegularExpressions;

namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>
/// Names the repository of a workspace as <c>owner/name</c> by reading the <c>origin</c> remote from the git configuration. It reads
/// files only, runs no process and never throws; an unknown repository is null.
/// </summary>
internal static partial class WorkspaceRepositoryResolver
{
    private const int MaxConfigBytes = 256 * 1024;

    /// <summary>Resolves the repository of a workspace.</summary>
    /// <param name="workspaceRoot">The absolute workspace path.</param>
    /// <returns>The repository as <c>owner/name</c>, or null when it cannot be determined.</returns>
    internal static string? Resolve(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return null;
        }

        try
        {
            var gitDirectory = FindGitDirectory(workspaceRoot);
            if (gitDirectory is null)
            {
                return null;
            }

            var commonFile = Path.Combine(gitDirectory, "commondir");
            if (File.Exists(commonFile))
            {
                var common = ReadCapped(commonFile)?.Trim();
                if (!string.IsNullOrEmpty(common))
                {
                    gitDirectory = Path.GetFullPath(common, gitDirectory);
                }
            }

            var config = ReadCapped(Path.Combine(gitDirectory, "config"));
            var url = config is null ? null : OriginUrl(config);
            return url is null ? null : ParseRepository(url);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Extracts <c>owner/name</c> from a remote URL in https, ssh or scp form.</summary>
    /// <param name="url">The remote URL.</param>
    /// <returns>The repository, or null when the URL has no owner and name.</returns>
    internal static string? ParseRepository(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var text = url.Trim();
        string path;
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            var afterAuthority = text.IndexOf('/', scheme + 3);
            if (afterAuthority < 0)
            {
                return null;
            }

            path = text[(afterAuthority + 1)..];
        }
        else
        {
            var colon = text.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                return null;
            }

            path = text[(colon + 1)..];
        }

        var query = path.IndexOfAny(['?', '#']);
        if (query >= 0)
        {
            path = path[..query];
        }

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        var name = segments[^1];
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        var owner = segments[^2];
        return SafeSegment().IsMatch(owner) && SafeSegment().IsMatch(name) ? $"{owner}/{name}" : null;
    }

    private static string? FindGitDirectory(string workspaceRoot)
    {
        var dotGit = Path.Combine(workspaceRoot, ".git");
        if (Directory.Exists(dotGit))
        {
            return dotGit;
        }

        if (!File.Exists(dotGit))
        {
            return null;
        }

        var pointer = ReadCapped(dotGit)?.Trim();
        const string Prefix = "gitdir:";
        if (pointer is null || !pointer.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var target = pointer[Prefix.Length..].Trim();
        return target.Length == 0 ? null : Path.GetFullPath(target, workspaceRoot);
    }

    private static string? OriginUrl(string config)
    {
        var inOrigin = false;
        foreach (var rawLine in config.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('['))
            {
                inOrigin = string.Equals(string.Concat(line.Where(static c => !char.IsWhiteSpace(c))), "[remote\"origin\"]", StringComparison.Ordinal);
                continue;
            }

            if (inOrigin && line.StartsWith("url", StringComparison.Ordinal))
            {
                var equals = line.IndexOf('=', StringComparison.Ordinal);
                if (equals > 0 && string.Equals(line[..equals].Trim(), "url", StringComparison.Ordinal))
                {
                    return line[(equals + 1)..].Trim();
                }
            }
        }

        return null;
    }

    private static string? ReadCapped(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[(int)Math.Min(stream.Length, MaxConfigBytes)];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, total);
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SafeSegment();
}
