using Npgsql;

namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// A step that must check the server before its SQL runs, for example a licence or a setting that SQL alone cannot report as a named error. The runner calls
/// <see cref="CheckAsync"/> inside the step's transaction, after the migrations lock is held and before <see cref="IMigrationStep.UpSql"/> runs.
/// </summary>
public interface IMigrationPrecondition
{
    /// <summary>Checks the precondition and throws <see cref="MigrationPreconditionException"/> when it does not hold.</summary>
    /// <param name="connection">The open connection of the step's transaction.</param>
    /// <param name="transaction">The step's transaction.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the precondition holds.</returns>
    /// <exception cref="MigrationPreconditionException">The precondition does not hold; the step is not applied.</exception>
    Task CheckAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken);
}
