using OmpOS.Workspace.Conventions;

namespace OmpOS.Workspace.Tests.Conventions;

/// <summary>
/// A file tree held in memory, with POSIX-style paths. It stands for the remote and cloud file trees that later phases implement <see cref="IConventionFileSource"/> over: nothing here touches the disk,
/// and it counts what discovery asks of it.
/// </summary>
internal sealed class InMemoryConventionFileSource : IConventionFileSource
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (ConventionDiagnosticKind Kind, string Problem)> _brokenFolders = new(StringComparer.Ordinal);

    /// <summary>Gets the folders that were listed, in order.</summary>
    public List<string> Listed { get; } = [];

    /// <summary>Gets the files that were read, in order.</summary>
    public List<string> Read { get; } = [];

    /// <summary>Adds a file.</summary>
    /// <param name="path">The absolute path.</param>
    /// <param name="text">The file text.</param>
    public void Add(string path, string text) => _files[path] = text;

    /// <summary>Makes listing a folder fail with a problem.</summary>
    /// <param name="path">The folder path.</param>
    /// <param name="kind">The kind of problem.</param>
    /// <param name="problem">The reason.</param>
    public void Break(string path, ConventionDiagnosticKind kind, string problem) => _brokenFolders[path] = (kind, problem);

    /// <inheritdoc/>
    public Task<ConventionListResult> ListAsync(string directory, int maxEntries, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Listed.Add(directory);
        if (_brokenFolders.TryGetValue(directory, out var broken))
        {
            return Task.FromResult(ConventionListResult.Fail(broken.Kind, broken.Problem));
        }

        var prefix = directory.TrimEnd('/') + "/";
        var entries = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var path in _files.Keys.Where(path => path.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var rest = path[prefix.Length..];
            var slash = rest.IndexOf('/', StringComparison.Ordinal);
            entries[slash < 0 ? rest : rest[..slash]] = slash >= 0;
        }

        return Task.FromResult(new ConventionListResult(
            [.. entries.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Take(maxEntries).Select(static pair => new ConventionEntry(pair.Key, pair.Value))]));
    }

    /// <inheritdoc/>
    public Task<ConventionReadResult> ReadTextAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Read.Add(path);
        if (!_files.TryGetValue(path, out var text))
        {
            return Task.FromResult(ConventionReadResult.Fail(ConventionDiagnosticKind.Unreadable, "the file does not exist"));
        }

        return Task.FromResult(
            System.Text.Encoding.UTF8.GetByteCount(text) > maxBytes
                ? ConventionReadResult.Fail(ConventionDiagnosticKind.TooLarge, "the file is larger than the limit")
                : ConventionReadResult.Read(text));
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_files.ContainsKey(path) || _files.Keys.Any(file => file.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal)));
    }
}
