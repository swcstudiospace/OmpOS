using System.ComponentModel.DataAnnotations;

namespace Autonogrammer.Api.Options;

/// <summary>
/// Binds the <c>Sessions</c> configuration section: which session store the API reads and writes. The store is required;
/// the host ships no default, so a missing key fails startup and names itself.
/// </summary>
internal sealed class SessionsOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Sessions";

    /// <summary>Gets the PostgreSQL (TimescaleDB) session store.</summary>
    public const string PostgresStore = "Postgres";

    /// <summary>Gets the in-process session store.</summary>
    public const string InMemoryStore = "InMemory";

    /// <summary>Gets or sets the store: <c>Postgres</c> or <c>InMemory</c>.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Sessions:Store is required. Set Sessions:Store to Postgres or InMemory.")]
    public string Store { get; set; } = string.Empty;

    /// <summary>Gets or sets the PostgreSQL settings. Read only when <see cref="Store"/> is <c>Postgres</c>.</summary>
    public SessionsPostgresOptions Postgres { get; set; } = new();

    /// <summary>Returns whether <paramref name="store"/> names a session store this host can serve.</summary>
    /// <param name="store">The configured store name.</param>
    /// <returns>True for <c>Postgres</c> or <c>InMemory</c>.</returns>
    public static bool IsSupportedStore(string? store) =>
        IsPostgresStore(store) || string.Equals(store, InMemoryStore, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns whether <paramref name="store"/> names the PostgreSQL session store.</summary>
    /// <param name="store">The configured store name.</param>
    /// <returns>True for <c>Postgres</c>.</returns>
    public static bool IsPostgresStore(string? store) => string.Equals(store, PostgresStore, StringComparison.OrdinalIgnoreCase);
}
