using OmpOS.Presentation.Mentions;

namespace OmpOS.Native.Tests.Composition.Mentions;

/// <summary>A file source over a fixed list of paths, standing in for the file tree of a remote session.</summary>
internal sealed class FixedMentionFiles : IMentionFileSource
{
    private readonly string[] _paths;

    /// <summary>Creates the source.</summary>
    /// <param name="paths">The workspace-relative file paths it lists.</param>
    internal FixedMentionFiles(string[] paths)
    {
        _paths = paths;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MentionFileEntry>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MentionFileEntry>>([.. _paths.Select(static path => new MentionFileEntry(path, false, 1))]);

    /// <inheritdoc />
    public Task<MentionContent> ReadAsync(string relativePath, int maxBytes, CancellationToken cancellationToken) =>
        Task.FromResult(new MentionContent("remote " + relativePath, 1, false));

    /// <inheritdoc />
    public Uri ToUri(string relativePath) => new("ssh://host/" + relativePath);
}
