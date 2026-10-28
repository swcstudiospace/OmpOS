using System.Globalization;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// A small text file the remote shell writes from the ssh stdin before omp starts, for example the settings overlay of a session. The
/// file is created with mode 0600 inside a directory of mode 0700.
/// </summary>
/// <param name="Path">The absolute POSIX path on the remote machine.</param>
/// <param name="Content">The file content.</param>
public sealed record RemoteFile(string Path, string Content)
{
    /// <summary>Describes the file by its path and size only.</summary>
    /// <returns>The description, without any content.</returns>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"RemoteFile {{ Path = {Path}, Length = {Content.Length} }}");
}
