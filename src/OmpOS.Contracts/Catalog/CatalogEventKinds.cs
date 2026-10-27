namespace OmpOS.Contracts.Catalog;

/// <summary>The SSE <c>event:</c> names of the user-level stream <c>GET /events</c>.</summary>
public static class CatalogEventKinds
{
    /// <summary>A session appeared, closed, reopened or changed in the catalog. The data is a <see cref="CatalogChangedEvent"/>.</summary>
    public const string CatalogChanged = "catalog_changed";

    /// <summary>A session's status changed. The data is a <see cref="SessionStatusEvent"/>.</summary>
    public const string SessionStatus = "session_status";

    /// <summary>A stage of a session changed. The data is a <see cref="OmpOS.Contracts.Agents.StageEvent"/>.</summary>
    public const string Stage = "stage";

    /// <summary>The requested resume position is older than the retained history. The data is a <see cref="CatalogResyncEvent"/>; the client reloads <c>GET /sessions</c>.</summary>
    public const string Resync = "resync";
}
