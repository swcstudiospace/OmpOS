using Microsoft.AspNetCore.Http.HttpResults;
using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Workspace;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>Lists one folder of the workspace. Secret names and links that leave the workspace folder are left out of the listing.</summary>
internal sealed class WorkspaceTreeReader
{
    /// <summary>The most entries one listing returns.</summary>
    public const int MaxEntries = 2000;

    private readonly WorkspaceAccess _access;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceTreeReader"/> class.</summary>
    /// <param name="access">What the endpoints may read.</param>
    public WorkspaceTreeReader(WorkspaceAccess access)
    {
        _access = access;
    }

    /// <summary>Lists a folder.</summary>
    /// <param name="path">The workspace-relative folder, or null or empty for the workspace folder itself.</param>
    /// <returns>The listing or the problem to answer with.</returns>
    public WorkspaceOutcome<WorkspaceTreeResponse> Read(string? path)
    {
        if (_access.ExistingRoot is not { } root)
        {
            return WorkspaceOutcome<WorkspaceTreeResponse>.Failure(NotConfigured());
        }

        var resolved = WorkspacePathResolver.Resolve(root, path, allowEmpty: true);
        if (resolved.Problem is not null || resolved.Value is not { } target)
        {
            return WorkspaceOutcome<WorkspaceTreeResponse>.Failure(resolved.Problem!);
        }

        if (!Directory.Exists(target.Real))
        {
            return WorkspacePathResolver.NotFound<WorkspaceTreeResponse>();
        }

        try
        {
            var entries = new List<WorkspaceTreeEntry>();
            var truncated = false;
            var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false, ReturnSpecialDirectories = false, AttributesToSkip = default };
            foreach (var entryPath in Directory.EnumerateFileSystemEntries(target.Real, "*", options))
            {
                var entry = ToEntry(target, entryPath);
                if (entry is null)
                {
                    continue;
                }

                if (entries.Count == MaxEntries)
                {
                    truncated = true;
                    break;
                }

                entries.Add(entry);
            }

            entries.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
            return WorkspaceOutcome<WorkspaceTreeResponse>.Success(new WorkspaceTreeResponse { Path = target.Relative, Entries = entries, Truncated = truncated });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return WorkspacePathResolver.NotFound<WorkspaceTreeResponse>();
        }
    }

    /// <summary>Builds the 404 answered when the API has no readable workspace folder.</summary>
    /// <returns>The problem.</returns>
    public static ProblemHttpResult NotConfigured() =>
        WebProblems.Of(StatusCodes.Status404NotFound, "workspace_not_configured", "Not found.", "The API has no readable workspace folder.");

    private static WorkspaceTreeEntry? ToEntry(ResolvedWorkspacePath folder, string entryPath)
    {
        var name = Path.GetFileName(entryPath);
        if (SecretFilePolicy.IsSecretName(name))
        {
            return null;
        }

        var real = entryPath;
        if (new FileInfo(entryPath).LinkTarget is not null)
        {
            try
            {
                real = folder.Root.Resolve(folder.Relative.Length == 0 ? name : folder.Relative + "/" + name);
            }
            catch (Exception exception) when (exception is WorkspacePathException or IOException)
            {
                return null;
            }

            if (SecretFilePolicy.IsSecretPath(Path.GetRelativePath(folder.Root.FullPath, real)))
            {
                return null;
            }
        }

        if (Directory.Exists(real))
        {
            return new WorkspaceTreeEntry { Name = name, Kind = WorkspaceEntryKind.Directory };
        }

        return File.Exists(real) ? new WorkspaceTreeEntry { Name = name, Kind = WorkspaceEntryKind.File, Size = new FileInfo(real).Length } : null;
    }
}
