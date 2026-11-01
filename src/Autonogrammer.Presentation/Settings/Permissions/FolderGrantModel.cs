namespace Autonogrammer.Presentation.Settings.Permissions;

/// <summary>Round-trips folder grants through <see cref="IFolderGrantSource"/>.</summary>
public sealed class FolderGrantModel
{
    private readonly IFolderGrantSource _source;

    /// <summary>Initializes a new instance of the <see cref="FolderGrantModel"/> class.</summary>
    /// <param name="source">The grant source.</param>
    public FolderGrantModel(IFolderGrantSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the grants.</summary>
    public IReadOnlyList<FolderGrantDraft> Grants => _source.List();

    /// <summary>Saves a grant and returns the stored copy.</summary>
    /// <param name="path">The folder.</param>
    /// <param name="access">The access mode.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The grant the source stored.</returns>
    public FolderGrantDraft Save(string path, FolderAccessMode access, FolderGrantScope scope)
    {
        var grant = new FolderGrantDraft(path, access, scope);
        _source.Grant(grant);
        return _source.List().First(item => string.Equals(item.Path, path, StringComparison.Ordinal));
    }

    /// <summary>Removes a grant.</summary>
    /// <param name="path">The folder.</param>
    public void Revoke(string path) => _source.Revoke(path);
}
