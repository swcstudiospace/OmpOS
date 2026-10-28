using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// OPS-02 bare-name PATH resolution. The test prepends a temporary directory to the process PATH, which every other test observes,
/// so it runs in its own collection with parallelization disabled.
/// </summary>
[Collection(ProcessPathSerialization.Name)]
public sealed class HarnessBarePathReadinessCheckTests
{
    /// <summary>Verifies a bare executable name found in a PATH directory reports healthy.</summary>
    [Fact]
    public async Task AcpAdapter_WithBareNameFoundOnPath_IsHealthy()
    {
        var testToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "autonogrammer-readiness-" + Guid.NewGuid().ToString("N"));
        var name = "omp-readiness-" + Guid.NewGuid().ToString("N");
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, name), string.Empty, testToken).ConfigureAwait(true);
            Environment.SetEnvironmentVariable("PATH", directory + Path.PathSeparator + originalPath);

            var result = await HarnessReadinessCheckTests.CheckAsync(HarnessReadinessCheckTests.AcpOptions(name)).ConfigureAwait(true);

            result.Status.ShouldBe(HealthStatus.Healthy);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(directory, recursive: true);
        }
    }
}
