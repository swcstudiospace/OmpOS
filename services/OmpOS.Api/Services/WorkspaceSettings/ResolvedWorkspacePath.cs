using OmpOS.Workspace;

namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>A request path that passed every check.</summary>
/// <param name="Relative">The workspace-relative path with <c>/</c> separators; empty for the workspace folder itself.</param>
/// <param name="Real">The real absolute path, with every symbolic link resolved.</param>
/// <param name="Root">The workspace root the path was resolved in.</param>
internal sealed record ResolvedWorkspacePath(string Relative, string Real, WorkspaceRoot Root);
