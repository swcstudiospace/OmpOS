namespace Autonogrammer.Presentation.Editor;

/// <summary>The size bounds of the asynchronous workspace file systems.</summary>
public static class WorkspaceFileLimits
{
    /// <summary>The default most bytes a file read returns: 1 MiB, the same cap omp applies to a remote <c>ssh://</c> read.</summary>
    public const int DefaultMaxReadBytes = 1024 * 1024;
}
