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
}
