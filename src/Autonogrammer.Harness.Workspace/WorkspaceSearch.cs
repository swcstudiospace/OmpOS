using System.Text;
using System.Text.RegularExpressions;

namespace Autonogrammer.Workspace;

/// <summary>
/// Text and regular-expression search over the files under a workspace root. Symbolic links and reparse points
/// are not followed, so a search never reads outside the root. The <c>.git</c> folder and files over
/// <see cref="MaxFileBytes"/> or containing NUL bytes are skipped. Semantic indexing is not provided.
/// </summary>
public sealed class WorkspaceSearch
{
    /// <summary>The hit limit used when a request does not set one.</summary>
    public const int DefaultMaxHits = 1000;

    /// <summary>The largest file that is searched. Larger files are skipped.</summary>
    public const long MaxFileBytes = 1024 * 1024;

    /// <summary>The time limit of one regular-expression match operation on one line.</summary>
    public static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(2);

    private const string GitFolderName = ".git";

    private readonly WorkspaceRoot _root;

    /// <summary>
    /// Creates a search over a workspace.
    /// </summary>
    /// <param name="root">The workspace root.</param>
    public WorkspaceSearch(WorkspaceRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
    }

    /// <summary>
    /// Searches every eligible file under the root.
    /// </summary>
    /// <param name="request">The search request.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The hits, or an error result for an empty pattern or an invalid or timed-out regular expression.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="SearchRequest.MaxHits"/> is less than 1.</exception>
    public Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaxHits < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.MaxHits, "The hit limit must be at least 1.");
        }
        return Task.Run(() => Search(request, cancellationToken), cancellationToken);
    }

    private SearchResult Search(SearchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Pattern))
        {
            return SearchResult.Failure("The search pattern must not be empty.");
        }

        Regex? regex = null;
        if (request.Kind == SearchKind.Regex)
        {
            try
            {
                var options = RegexOptions.CultureInvariant
                    | (request.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
                regex = new Regex(request.Pattern, options, RegexMatchTimeout);
            }
            catch (ArgumentException exception)
            {
                return SearchResult.Failure($"Invalid regular expression: {exception.Message}");
            }
        }

        var hits = new List<SearchHit>();
        foreach (var relativePath in EnumerateFiles(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = ReadSearchableText(Path.Combine(_root.FullPath, relativePath));
            if (content is null)
            {
                continue;
            }

            var portablePath = WorkspaceFiles.ToPortable(relativePath);
            var lineNumber = 0;
            foreach (var line in content.Split('\n'))
            {
                lineNumber++;
                try
                {
                    foreach (var matchText in MatchesIn(line, request, regex))
                    {
                        if (hits.Count == request.MaxHits)
                        {
                            return new SearchResult(hits, Truncated: true, Error: null);
                        }

                        hits.Add(new SearchHit(portablePath, lineNumber, matchText));
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    return SearchResult.Failure(
                        $"The regular expression timed out after {RegexMatchTimeout.TotalSeconds} seconds at line {lineNumber} of '{portablePath}'.");
                }
            }
        }

        return new SearchResult(hits, Truncated: false, Error: null);
    }

    private static IEnumerable<string> MatchesIn(string line, SearchRequest request, Regex? regex)
    {
        if (regex is not null)
        {
            foreach (Match match in regex.Matches(line))
            {
                if (match.Length > 0)
                {
                    yield return match.Value;
                }
            }

            yield break;
        }

        var comparison = request.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var index = 0;
        while ((index = line.IndexOf(request.Pattern, index, comparison)) >= 0)
        {
            yield return line.Substring(index, request.Pattern.Length);
            index += request.Pattern.Length;
        }
    }

    private List<string> EnumerateFiles(CancellationToken cancellationToken)
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(_root.FullPath);

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = new DirectoryInfo(pending.Pop());
            foreach (var entry in directory.EnumerateFileSystemInfos("*", options))
            {
                if (entry is DirectoryInfo subdirectory)
                {
                    if (!string.Equals(subdirectory.Name, GitFolderName, StringComparison.Ordinal))
                    {
                        pending.Push(subdirectory.FullName);
                    }
                }
                else
                {
                    files.Add(Path.GetRelativePath(_root.FullPath, entry.FullName));
                }
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static string? ReadSearchableText(string fullPath)
    {
        try
        {
            if (new FileInfo(fullPath).Length > MaxFileBytes)
            {
                return null;
            }

            var bytes = File.ReadAllBytes(fullPath);
            if (Array.IndexOf(bytes, (byte)0) >= 0)
            {
                return null;
            }

            return Encoding.UTF8.GetString(bytes);
        }
        catch (IOException)
        {
            // The file was removed or locked while the search ran.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
