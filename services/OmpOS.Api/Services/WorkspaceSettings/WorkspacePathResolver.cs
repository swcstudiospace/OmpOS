using OmpOS.Workspace;

namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>
/// Turns the <c>path</c> query value of the tree and file endpoints into a real path inside the workspace folder. A path with a <c>..</c> segment, a
/// leading separator, a backslash or a control character is 400; a path that resolves through a link outside the folder is 422; a path with a secret
/// name (<see cref="SecretFilePolicy"/>), also through a link, is answered as if it were absent.
/// </summary>
internal static class WorkspacePathResolver
{
    /// <summary>The longest accepted path, in characters.</summary>
    public const int MaxPathLength = 4096;

    private const string InvalidPathCode = "invalid_workspace_path";

    /// <summary>The problem code of a path that does not exist or is not shown.</summary>
    public const string NotFoundCode = "workspace_path_not_found";

    /// <summary>Checks and resolves a path.</summary>
    /// <param name="root">The existing workspace folder.</param>
    /// <param name="path">The requested path, or null or empty for the workspace folder itself when <paramref name="allowEmpty"/> is true.</param>
    /// <param name="allowEmpty">Whether the workspace folder itself is a valid answer.</param>
    /// <returns>The resolved path or the problem to answer with.</returns>
    public static WorkspaceOutcome<ResolvedWorkspacePath> Resolve(string root, string? path, bool allowEmpty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        WorkspaceRoot workspace;
        try
        {
            workspace = new WorkspaceRoot(root);
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return Fail(StatusCodes.Status404NotFound, "workspace_not_configured", "The workspace folder of the API cannot be read.");
        }

        if (string.IsNullOrEmpty(path))
        {
            return allowEmpty
                ? WorkspaceOutcome<ResolvedWorkspacePath>.Success(new ResolvedWorkspacePath(string.Empty, workspace.FullPath, workspace))
                : Fail(StatusCodes.Status400BadRequest, InvalidPathCode, "The path query value is required.");
        }

        if (path.Length > MaxPathLength || path.StartsWith('/') || path.Contains('\\', StringComparison.Ordinal) || path.Any(char.IsControl))
        {
            return Fail(StatusCodes.Status400BadRequest, InvalidPathCode, "The path must be a relative path with / separators and no control characters.");
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(static segment => !string.Equals(segment, ".", StringComparison.Ordinal)).ToArray();
        if (segments.Any(static segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            return Fail(StatusCodes.Status400BadRequest, InvalidPathCode, "The path must not contain .. segments.");
        }

        var relative = string.Join('/', segments);
        if (relative.Length == 0)
        {
            return allowEmpty
                ? WorkspaceOutcome<ResolvedWorkspacePath>.Success(new ResolvedWorkspacePath(string.Empty, workspace.FullPath, workspace))
                : Fail(StatusCodes.Status400BadRequest, InvalidPathCode, "The path must name a file.");
        }

        if (SecretFilePolicy.IsSecretPath(relative))
        {
            return NotFound();
        }

        string real;
        try
        {
            real = workspace.Resolve(relative);
        }
        catch (Exception exception) when (exception is WorkspacePathException or IOException)
        {
            return Fail(StatusCodes.Status422UnprocessableEntity, "path_outside_workspace", "The path leaves the workspace folder.");
        }

        var realRelative = Path.GetRelativePath(workspace.FullPath, real);
        return SecretFilePolicy.IsSecretPath(realRelative) ? NotFound() : WorkspaceOutcome<ResolvedWorkspacePath>.Success(new ResolvedWorkspacePath(relative, real, workspace));
    }

    /// <summary>Builds the 404 of a path that does not exist, is not what the endpoint reads, or is never shown.</summary>
    /// <typeparam name="T">The outcome value type.</typeparam>
    /// <returns>The failed outcome.</returns>
    public static WorkspaceOutcome<T> NotFound<T>() =>
        WorkspaceOutcome<T>.Failure(WebProblems.Of(StatusCodes.Status404NotFound, NotFoundCode, "Not found.", "The path does not exist in the workspace."));

    private static WorkspaceOutcome<ResolvedWorkspacePath> NotFound() => NotFound<ResolvedWorkspacePath>();

    private static WorkspaceOutcome<ResolvedWorkspacePath> Fail(int status, string code, string detail) =>
        WorkspaceOutcome<ResolvedWorkspacePath>.Failure(WebProblems.Of(status, code, status == StatusCodes.Status400BadRequest ? "Bad request." : "Request failed.", detail));
}
