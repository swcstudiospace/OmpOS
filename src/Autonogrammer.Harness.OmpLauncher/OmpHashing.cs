using System.Security.Cryptography;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>Hashes executables for the state record and for the active-install check.</summary>
internal static class OmpHashing
{
    /// <summary>Returns the hex SHA-256 of a file. The file is opened for reading while it may be running.</summary>
    /// <param name="path">The file to hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The uppercase hex digest.</returns>
    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        var digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(digest);
    }
}
