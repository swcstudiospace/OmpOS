using System.Security.Cryptography;

namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>Hashes file content for the checkpoint store.</summary>
internal static class CheckpointHash
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>Tells whether content is treated as binary: it holds a NUL byte.</summary>
    /// <param name="content">The file bytes.</param>
    /// <returns><see langword="true"/> for binary content.</returns>
    public static bool IsBinary(ReadOnlySpan<byte> content) => content.Contains((byte)0);

    /// <summary>
    /// Hashes content for comparison: text without its byte order mark and with CRLF read as LF, so a file that an editor re-saved with other
    /// line endings is still the file the agent left; binary content as it is.
    /// </summary>
    /// <param name="content">The file bytes.</param>
    /// <returns>The lowercase hexadecimal SHA-256.</returns>
    public static string Of(ReadOnlySpan<byte> content)
    {
        if (IsBinary(content))
        {
            return Raw(content);
        }

        if (content.StartsWith(Bom))
        {
            content = content[Bom.Length..];
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var start = 0;
        for (var index = 0; index + 1 < content.Length; index++)
        {
            if (content[index] == (byte)'\r' && content[index + 1] == (byte)'\n')
            {
                hash.AppendData(content[start..index]);
                start = index + 1;
            }
        }

        hash.AppendData(content[start..]);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Hashes the exact bytes. It names the stored copy of a file.</summary>
    /// <param name="content">The file bytes.</param>
    /// <returns>The lowercase hexadecimal SHA-256.</returns>
    public static string Raw(ReadOnlySpan<byte> content) => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>Names the folder of one session: the hash of its id, so no session id can reach outside the store.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <returns>A 32 character lowercase hexadecimal name.</returns>
    public static string SessionFolder(string sessionId) => Raw(System.Text.Encoding.UTF8.GetBytes(sessionId))[..32];
}
