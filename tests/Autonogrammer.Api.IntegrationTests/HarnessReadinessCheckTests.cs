using System.Net;
using Autonogrammer.Api.Health;
using Autonogrammer.Api.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// OPS-02 harness readiness: the in-memory adapter is ready, and the ACP adapter is ready only when its omp path names an existing
/// file. The probe never starts omp, so every case here is decided by the filesystem alone.
/// </summary>
public sealed class HarnessReadinessCheckTests
{
    /// <summary>Verifies the in-memory adapter reports healthy with no external process.</summary>
    [Fact]
    public async Task InMemoryAdapter_IsHealthy()
    {
        var options = new HarnessOptions { Adapter = HarnessOptions.InMemoryAdapter };

        var result = await CheckAsync(options).ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Verifies an ACP adapter whose absolute omp path does not exist reports unhealthy, naming the setting and the path tried.</summary>
    [Fact]
    public async Task AcpAdapter_WithNonexistentAbsolutePath_IsUnhealthyNamingSettingAndPath()
    {
        var missing = Path.Combine(Path.GetTempPath(), "autonogrammer-readiness-" + Guid.NewGuid().ToString("N"), "omp");

        var result = await CheckAsync(AcpOptions(missing)).ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        var description = result.Description.ShouldNotBeNull();
        description.ShouldContain("Harness:Acp:OmpPath");
        description.ShouldContain(missing);
    }

    /// <summary>Verifies an ACP adapter whose absolute omp path is an existing file reports healthy. Environment.ProcessPath is always an existing file.</summary>
    [Fact]
    public async Task AcpAdapter_WithAbsolutePathToExistingFile_IsHealthy()
    {
        var existing = Environment.ProcessPath ?? throw new InvalidOperationException("The test host has no process path.");

        var result = await CheckAsync(AcpOptions(existing)).ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Verifies a bare executable name absent from every PATH directory reports unhealthy, naming the setting and the name.</summary>
    [Fact]
    public async Task AcpAdapter_WithBareNameMissingFromPath_IsUnhealthyNamingSettingAndName()
    {
        var name = "omp-readiness-missing-" + Guid.NewGuid().ToString("N");

        var result = await CheckAsync(AcpOptions(name)).ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        var description = result.Description.ShouldNotBeNull();
        description.ShouldContain("Harness:Acp:OmpPath");
        description.ShouldContain(name);
    }

    /// <summary>Verifies the ready endpoint answers 503 when the ACP adapter's omp path does not exist, so the check is wired into readiness.</summary>
    [Fact]
    public async Task ReadyEndpoint_AcpAdapterWithMissingOmp_Returns503()
    {
        var missing = Path.Combine(Path.GetTempPath(), "autonogrammer-readiness-" + Guid.NewGuid().ToString("N"), "omp");
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Harness:Adapter"] = HarnessOptions.AcpAdapter,
            ["Harness:Acp:OmpPath"] = missing,
        };
        using var factory = new ApiFactory(config);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>Verifies the ready endpoint answers 200 when the ACP adapter's omp path is an existing file.</summary>
    [Fact]
    public async Task ReadyEndpoint_AcpAdapterWithExistingOmpFile_Returns200()
    {
        var existing = Environment.ProcessPath ?? throw new InvalidOperationException("The test host has no process path.");
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Harness:Adapter"] = HarnessOptions.AcpAdapter,
            ["Harness:Acp:OmpPath"] = existing,
        };
        using var factory = new ApiFactory(config);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    internal static HarnessOptions AcpOptions(string ompPath)
    {
        var options = new HarnessOptions { Adapter = HarnessOptions.AcpAdapter };
        options.Acp.OmpPath = ompPath;
        return options;
    }

    internal static async Task<HealthCheckResult> CheckAsync(HarnessOptions options)
    {
        var check = new HarnessReadinessCheck(Microsoft.Extensions.Options.Options.Create(options));
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("harness", check, null, null) };
        return await check.CheckHealthAsync(context, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
