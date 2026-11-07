#pragma warning disable MA0048 // The plan owns this file for the port, the result and the state names.
using Autonogrammer.Contracts.Cowork.Projects;

namespace Autonogrammer.Cowork.Projects;

/// <summary>Degraded states for project sync.</summary>
public static class ProjectSyncState
{
    /// <summary>The last call returned 200.</summary>
    public const string Synced = "Synced";

    /// <summary>No bearer token. Cowork asks the user to sign in.</summary>
    public const string SignedOut = "SignedOut";

    /// <summary>The server returned 401. The cached copy is read-only.</summary>
    public const string TokenExpired = "TokenExpired";

    /// <summary>The server could not be reached. Projects are read-only.</summary>
    public const string Offline = "Offline";

    /// <summary>The server returned 429.</summary>
    public const string RateLimited = "RateLimited";

    /// <summary>The project is archived.</summary>
    public const string ProjectArchived = "ProjectArchived";
}

/// <summary>The outcome of one project API call.</summary>
/// <typeparam name="T">The payload.</typeparam>
/// <param name="Succeeded">True on a 2xx response.</param>
/// <param name="StatusCode">The HTTP status, or null when the call did not reach a status.</param>
/// <param name="Offline">True when the call did not reach the server.</param>
/// <param name="Value">The payload, or default.</param>
/// <param name="Reason">A short reason, or null on success.</param>
/// <param name="State">A <see cref="ProjectSyncState"/> value.</param>
public sealed record ProjectApiResult<T>(bool Succeeded, int? StatusCode, bool Offline, T? Value, string? Reason, string State);

/// <summary>
/// Syncs project metadata, never folders, and the account-global instructions channel.
/// </summary>
public interface IProjectApi
{
    /// <summary>Gets <c>/cowork/projects</c>.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The metadata records.</returns>
    Task<ProjectApiResult<IReadOnlyList<ProjectSyncRecord>>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Puts one record to <c>/cowork/projects/{id}</c>. The body has no folders member.</summary>
    /// <param name="project">The metadata.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    Task<ProjectApiResult<ProjectSyncRecord>> UpsertAsync(ProjectSyncRecord project, CancellationToken cancellationToken);

    /// <summary>Posts <c>/cowork/projects/{id}/archive</c>.</summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    Task<ProjectApiResult<bool>> ArchiveAsync(string id, CancellationToken cancellationToken);

    /// <summary>Deletes <c>/cowork/projects/{id}</c>.</summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    Task<ProjectApiResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>Gets <c>/cowork/instructions/global</c>.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The instructions text.</returns>
    Task<ProjectApiResult<string>> GetGlobalInstructionsAsync(CancellationToken cancellationToken);

    /// <summary>Puts <c>/cowork/instructions/global</c>. The 8 KiB limit is enforced before send.</summary>
    /// <param name="instructions">The instructions.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    Task<ProjectApiResult<bool>> PutGlobalInstructionsAsync(string instructions, CancellationToken cancellationToken);
}
