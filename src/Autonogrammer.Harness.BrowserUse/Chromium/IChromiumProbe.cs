namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>The file-system and version seam the locator uses. Tests supply a fake; production reads the disk and <c>--version</c>.</summary>
public interface IChromiumProbe
{
    /// <summary>Returns whether <paramref name="path"/> exists as a file.</summary>
    /// <param name="path">The candidate path.</param>
    /// <returns>True when the file exists.</returns>
    bool Exists(string path);

    /// <summary>Returns the symlink target, or null when <paramref name="path"/> is not a symlink.</summary>
    /// <param name="path">The candidate path.</param>
    /// <returns>The link target, or null.</returns>
    string? ReadLink(string path);

    /// <summary>Reads the Chromium major version, or null when it cannot be read.</summary>
    /// <param name="path">The binary path.</param>
    /// <returns>The major version, or null.</returns>
    int? ReadMajorVersion(string path);

    /// <summary>Lists immediate child directory names, or an empty list when <paramref name="path"/> is missing.</summary>
    /// <param name="path">The directory.</param>
    /// <returns>The child names, not full paths.</returns>
    IReadOnlyList<string> ListDirectories(string path);
}
