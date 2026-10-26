namespace OmpOS.Api.Options;

/// <summary>
/// Binds the <c>Sessions:Postgres</c> configuration section. Startup validation requires <see cref="ConnectionString"/> when
/// <c>Sessions:Store</c> is <c>Postgres</c>; the value is never logged.
/// </summary>
internal sealed class SessionsPostgresOptions
{
    /// <summary>Gets or sets the Npgsql connection string. Required when <c>Sessions:Store</c> is <c>Postgres</c>.</summary>
    public string? ConnectionString { get; set; }
}
