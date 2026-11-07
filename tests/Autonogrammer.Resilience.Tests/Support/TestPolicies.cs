namespace Autonogrammer.Resilience.Tests;

/// <summary>The shipped defaults of the three components, wired to the test fault types.</summary>
internal static class TestPolicies
{
    /// <summary>Gets the PostgreSQL defaults.</summary>
    /// <returns>The policy.</returns>
    public static ResiliencePolicy Postgres() =>
        new PostgresResilienceOptions().ToPolicy(IsTransient, IsSerializationFailure);

    /// <summary>Gets the Dragonfly defaults.</summary>
    /// <returns>The policy.</returns>
    public static ResiliencePolicy Dragonfly() =>
        new DragonflyResilienceOptions().ToPolicy(IsTransient);

    /// <summary>Gets the ledger defaults.</summary>
    /// <returns>The policy.</returns>
    public static ResiliencePolicy Ledger() =>
        new LedgerResilienceOptions().ToPolicy(IsTransient);

    private static bool IsTransient(Exception exception) => exception is TransientFault;

    private static bool IsSerializationFailure(Exception exception) => exception is SerializationFault;
}
