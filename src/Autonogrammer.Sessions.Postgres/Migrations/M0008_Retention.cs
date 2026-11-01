using Npgsql;

namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// M0008: the retention policies of the two telemetry-class hypertables: <c>dispatch_events</c> keeps 30 days and <c>dispatch_audit</c> keeps 400 days. User content
/// has no automatic limit. <c>add_retention_policy</c> is a feature of the Timescale licence, not of the Apache edition, so the step checks
/// <c>timescaledb.license</c> first and fails with the named error <c>retention_requires_timescale_license</c> when the server is not on the <c>timescale</c> licence.
/// An Apache-edition server runs retention from a one-shot <c>Autonogrammer.Api retention</c> with the migrator role instead.
/// </summary>
internal sealed class M0008_Retention : IMigrationStep, IMigrationPrecondition
{
    /// <summary>The stable error code of a server that lacks the Timescale licence.</summary>
    internal const string LicenseErrorCode = "retention_requires_timescale_license";

    private const string Up = """
        SELECT add_retention_policy('dispatch_events', INTERVAL '30 days', if_not_exists => TRUE);
        SELECT add_retention_policy('dispatch_audit', INTERVAL '400 days', if_not_exists => TRUE);
        """;

    private const string Down = """
        SELECT remove_retention_policy('dispatch_audit', if_exists => TRUE);
        SELECT remove_retention_policy('dispatch_events', if_exists => TRUE);
        """;

    private const string LicenseSql = "SELECT current_setting('timescaledb.license', true)";

    /// <inheritdoc />
    public int Version => 8;

    /// <inheritdoc />
    public string Name => "retention";

    /// <inheritdoc />
    public string UpSql => Up;

    /// <inheritdoc />
    public string DownSql => Down;

    /// <inheritdoc />
    public async Task CheckAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = LicenseSql;
        var license = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (!string.Equals(license, "timescale", StringComparison.Ordinal))
        {
            throw new MigrationPreconditionException(
                LicenseErrorCode,
                "add_retention_policy needs the Timescale licence ('timescaledb.license' = 'timescale'), but this server reports "
                + $"'{license ?? "not loaded"}'. Run retention from the one-shot 'Autonogrammer.Api retention' with the migrator role, or use the Timescale edition.");
        }
    }
}
