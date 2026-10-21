namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// The PostgreSQL database that workflow-enabled host tests use. With <c>Workflows:Enabled=true</c> the host creates the session
/// schema at startup, so those tests need a real server. <c>OMPOS_TEST_POSTGRES_CONNECTION</c> names one: a CI service or a
/// developer database. When the variable is unset the helper throws, so the tests fail loudly instead of skipping.
/// </summary>
internal static class PostgresTestDatabase
{
    /// <summary>Gets the environment variable that names the test database.</summary>
    public const string EnvironmentVariable = "OMPOS_TEST_POSTGRES_CONNECTION";

    /// <summary>Gets the configuration key the API reads the connection string from.</summary>
    public const string ConnectionStringKey = "Sessions:Postgres:ConnectionString";

    /// <summary>Gets the test database connection string.</summary>
    /// <returns>The connection string from <see cref="EnvironmentVariable"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the variable is unset or blank.</exception>
    public static string ConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariable);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{EnvironmentVariable} must name a PostgreSQL database for workflow-enabled API tests.")
            : value;
    }
}
