using System.ComponentModel.DataAnnotations;

namespace OmpOS.Api.Options;

/// <summary>
/// Binds the <c>Coordination</c> configuration section: which backend holds run leases, idempotency records and session
/// signals. The store is required; the host ships no default, so a missing key fails startup and names itself.
/// </summary>
internal sealed class CoordinationOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Coordination";

    /// <summary>Gets the configuration key of the Dragonfly connection string.</summary>
    public const string DragonflyConnectionStringKey = "Coordination:Dragonfly:ConnectionString";

    /// <summary>Gets the Dragonfly (Redis-protocol) coordination backend.</summary>
    public const string DragonflyStore = "Dragonfly";

    /// <summary>Gets the in-process coordination backend.</summary>
    public const string InMemoryStore = "InMemory";

    /// <summary>Gets or sets the store: <c>Dragonfly</c> or <c>InMemory</c>.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Coordination:Store is required. Set Coordination:Store to Dragonfly or InMemory.")]
    public string Store { get; set; } = string.Empty;

    /// <summary>Gets or sets the Dragonfly settings. Read only when <see cref="Store"/> is <c>Dragonfly</c>.</summary>
    public CoordinationDragonflyOptions Dragonfly { get; set; } = new();

    /// <summary>Returns whether <paramref name="store"/> names a coordination backend this host can serve.</summary>
    /// <param name="store">The configured store name.</param>
    /// <returns>True for <c>Dragonfly</c> or <c>InMemory</c>.</returns>
    public static bool IsSupportedStore(string? store) =>
        IsDragonflyStore(store) || string.Equals(store, InMemoryStore, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns whether <paramref name="store"/> names the Dragonfly coordination backend.</summary>
    /// <param name="store">The configured store name.</param>
    /// <returns>True for <c>Dragonfly</c>.</returns>
    public static bool IsDragonflyStore(string? store) => string.Equals(store, DragonflyStore, StringComparison.OrdinalIgnoreCase);
}
