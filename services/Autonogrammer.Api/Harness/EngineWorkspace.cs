using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Api.Harness;

/// <summary>
/// Creates the working directory of a session's engine under <c>Harness:Acp:WorkspaceRoot</c>: <c>&lt;WorkspaceRoot&gt;/&lt;sessionId&gt;</c>, readable
/// only by the API's user. The session id comes from a request path, so the name is checked before it reaches the file system.
/// </summary>
internal static class EngineWorkspace
{
    /// <summary>The longest session id accepted as a directory name.</summary>
    internal const int MaxSessionIdLength = 128;

    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <summary>
    /// Returns whether <paramref name="sessionId"/> is a safe directory name: 1 to <see cref="MaxSessionIdLength"/> ASCII letters, digits, hyphens
    /// and underscores. A separator, a dot, a drive colon or a NUL can therefore never appear, so the name cannot leave the workspace root.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>True when the identifier uses only the session identifier character set.</returns>
    internal static bool IsValidSessionId(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || sessionId.Length > MaxSessionIdLength)
        {
            return false;
        }

        foreach (var character in sessionId)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Creates, when missing, the session's directory under <paramref name="workspaceRoot"/> and restricts it to the owner (mode 0700 on Unix).
    /// A missing root is created owner-only; an existing root keeps its permissions. A session directory that is a link is refused.
    /// </summary>
    /// <param name="workspaceRoot">The absolute workspace root.</param>
    /// <param name="sessionId">The session identifier; it names the directory and must satisfy <see cref="IsValidSessionId"/>.</param>
    /// <returns>The absolute path of the session's directory.</returns>
    /// <exception cref="ArgumentException">The identifier is not a safe directory name or the root is not absolute.</exception>
    /// <exception cref="ProcessStartHarnessException">The directory could not be created or is a link.</exception>
    internal static string EnsureSessionDirectory(string workspaceRoot, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (!Path.IsPathFullyQualified(workspaceRoot))
        {
            throw new ArgumentException("The workspace root must be an absolute path.", nameof(workspaceRoot));
        }

        if (!IsValidSessionId(sessionId))
        {
            throw new ArgumentException("The session identifier cannot name an engine working directory.", nameof(sessionId));
        }

        var path = Path.Combine(workspaceRoot, sessionId);
        try
        {
            CreateDirectory(workspaceRoot, restrict: false);
            if (new DirectoryInfo(path).LinkTarget is not null)
            {
                throw new ProcessStartHarnessException("The engine working directory of the session is a link; it was not used.");
            }

            CreateDirectory(path, restrict: true);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProcessStartHarnessException("Could not create the engine working directory of the session under Harness:Acp:WorkspaceRoot.", ex);
        }
    }

    private static void CreateDirectory(string directory, bool restrict)
    {
        if (OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(directory);
            return;
        }

        // A directory that has to be created is created owner-only. Only the session's own directory is re-restricted when it already
        // exists; the operator's workspace root keeps the permissions the operator gave it.
        _ = Directory.CreateDirectory(directory, OwnerOnly);
        if (restrict)
        {
            File.SetUnixFileMode(directory, OwnerOnly);
        }
    }
}
