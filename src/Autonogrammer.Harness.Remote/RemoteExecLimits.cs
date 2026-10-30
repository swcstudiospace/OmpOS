namespace Autonogrammer.Harness.Remote;

/// <summary>The output caps of the remote file scripts.</summary>
internal static class RemoteExecLimits
{
    /// <summary>The most stdout bytes of a directory listing: 5000 names of up to 255 bytes with their marker.</summary>
    public const int ListingBytes = 2 * 1024 * 1024;

    /// <summary>The most stdout bytes of a script that only answers with a status line or nothing.</summary>
    public const int SmallAnswerBytes = 8 * 1024;
}
