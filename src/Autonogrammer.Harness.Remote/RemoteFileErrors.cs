using System.Globalization;
using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Harness.Remote;

/// <summary>Turns a failed remote file script into the exception the workspace file system contract documents.</summary>
internal static class RemoteFileErrors
{
    /// <summary>Creates the exception for a failed script.</summary>
    /// <param name="result">The result of the script.</param>
    /// <param name="path">The workspace-relative path the script was about; empty for the root.</param>
    /// <param name="target">Whether the path was a directory or a file.</param>
    /// <returns>The exception to throw. It never holds file content.</returns>
    public static Exception For(RemoteExecResult result, string path, RemoteFileTarget target)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(path);
        if (result.IsConnectionFailure)
        {
            return new IOException(result.DescribeConnectionFailure());
        }

        var shown = path.Length == 0 ? "the workspace root" : "'" + path + "'";
        return result.ExitCode switch
        {
            RemoteScripts.ExitRootMissing => new DirectoryNotFoundException("The remote workspace folder does not exist."),
            RemoteScripts.ExitNotFound => target == RemoteFileTarget.Directory
                ? (Exception)new DirectoryNotFoundException("No directory exists at " + shown + ".")
                : new FileNotFoundException("No file exists at " + shown + "."),
            RemoteScripts.ExitNotDirectory => new DirectoryNotFoundException(shown + " is not a directory."),
            RemoteScripts.ExitEscapes => new UnauthorizedAccessException(shown + " resolves outside the workspace root."),
            RemoteScripts.ExitUnverifiable => new UnauthorizedAccessException("The remote machine cannot confirm where " + shown + " really lies (no realpath or readlink)."),
            RemoteScripts.ExitNotRegular => new FileNotFoundException(shown + " is not a regular file."),
            RemoteScripts.ExitStale => new StaleWorkspaceFileException(shown + " changed on the remote machine after it was read; nothing was changed."),
            RemoteScripts.ExitExists => new IOException(shown + " already exists; nothing was written."),
            _ => new IOException(Other(result)),
        };
    }

    private static string Other(RemoteExecResult result)
    {
        var detail = RemoteDiagnosisCatalog.Excerpt(result.Error);
        var code = result.ExitCode.ToString(CultureInfo.InvariantCulture);
        return "The remote command failed (exit code " + code + ")" + (detail is null ? "." : ": " + detail);
    }
}
