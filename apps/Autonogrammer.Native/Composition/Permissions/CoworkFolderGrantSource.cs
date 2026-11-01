using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.Isolation;

namespace Autonogrammer.Native.Composition.Permissions;

/// <summary>
/// Reads Cowork folder grants and maps them to isolation grants. A read-only grant is not a write bind.
/// NotificationRequest and INotificationSink live in Harness.Notifications. Presentation never references them:
/// the bridge sits in Native and Native.Views.
/// </summary>
internal sealed class CoworkFolderGrantSource
{
    private readonly IReadOnlyList<CoworkFolderGrant> _grants;

    /// <summary>Creates a source over an already loaded grant list.</summary>
    /// <param name="grants">The grants. Null is none.</param>
    public CoworkFolderGrantSource(IReadOnlyList<CoworkFolderGrant>? grants = null)
    {
        _grants = grants ?? [];
    }

    /// <summary>Gets the grants this source was given.</summary>
    public IReadOnlyList<CoworkFolderGrant> Grants => _grants;

    /// <summary>Maps grants to isolation binds. Only read-write grants get a bind.</summary>
    /// <returns>One isolation grant per folder. Read-only entries have <see cref="IsolationGrant.ReadWrite"/> false.</returns>
    public IReadOnlyList<IsolationGrant> IsolationGrants() =>
        [.. _grants.Select(static grant => new IsolationGrant(grant.Path, grant.Access == CoworkFolderAccess.ReadWrite))];
}
