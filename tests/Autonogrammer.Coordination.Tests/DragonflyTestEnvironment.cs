using Autonogrammer.Coordination.Dragonfly;
using Autonogrammer.Resilience;

namespace Autonogrammer.Coordination.Tests;

/// <summary>Reads the Dragonfly test server and generates collision-free ids. The Dragonfly tests never skip.</summary>
internal static class DragonflyTestEnvironment
{
    /// <summary>The environment variable that names the Dragonfly test server.</summary>
    public const string ConnectionEnvironmentVariable = "AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION";

    /// <summary>Returns the StackExchange.Redis connection string, or fails the test when it is not configured.</summary>
    /// <returns>The non-blank connection string.</returns>
    /// <exception cref="InvalidOperationException">The variable is not set.</exception>
    public static string RequireConnectionString()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        return string.IsNullOrWhiteSpace(connection)
            ? throw new InvalidOperationException("AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION is not set; run scripts/test-services.sh up")
            : connection;
    }

    /// <summary>Returns a random identifier, so reruns against a shared server never collide.</summary>
    /// <returns>A session-like identifier that is unique per call.</returns>
    public static string NewId()
    {
        return "autonogrammer-test-" + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Returns the options for stores that talk to the real test server. The production deadline of 250 ms is for a dedicated service; the shared
    /// test host is heavily loaded, so the live tests use a deadline that measures behaviour, not host speed. Fail-mode tests use the defaults.
    /// </summary>
    /// <returns>The lenient options.</returns>
    public static DragonflyResilienceOptions LiveOptions()
    {
        return new DragonflyResilienceOptions { OpMs = 10_000, ConnectSeconds = 15 };
    }

    /// <summary>Returns a resilience wrapper built from <see cref="LiveOptions"/>.</summary>
    /// <returns>A fresh wrapper, so each test has its own breaker.</returns>
    public static DragonflyResilience LiveResilience()
    {
        return DragonflyResilience.Create(LiveOptions());
    }
}
