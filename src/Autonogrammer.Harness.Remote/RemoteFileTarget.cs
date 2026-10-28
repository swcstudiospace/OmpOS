namespace Autonogrammer.Harness.Remote;

/// <summary>What a remote file operation was about, so a "not found" answer becomes the right exception.</summary>
internal enum RemoteFileTarget
{
    /// <summary>A directory.</summary>
    Directory,

    /// <summary>A file.</summary>
    File,
}
