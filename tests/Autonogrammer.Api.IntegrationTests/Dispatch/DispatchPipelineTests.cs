using Autonogrammer.Api.Hosting;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Verifies connect timeout metadata and that the host order matches Program.cs.</summary>
public sealed class DispatchPipelineTests
{
    [Fact]
    public async Task The_connect_endpoint_names_the_NoTimeout_policy()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct, enabled: false).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var sources = host.Services.GetRequiredService<EndpointDataSource>();
        var connect = sources.Endpoints.Single(endpoint => endpoint.DisplayName?.Contains("/connect", StringComparison.Ordinal) == true);
        var timeout = connect.Metadata.GetMetadata<RequestTimeoutAttribute>();

        timeout.ShouldNotBeNull();
        timeout.PolicyName.ShouldBe(HostingPolicies.NoTimeout);
    }

    [Fact]
    public void The_host_pipeline_matches_Program()
    {
        var expected = new[]
        {
            "UseHostingHardening",
            "UseAuthentication",
            "UseRateLimiter",
            "UseAuthorization",
            "DispatchDisabledMiddleware",
            "MapDispatchEndpoints",
        };

        DispatchApiHost.PipelineOrder.ShouldBe(expected);
        var program = File.ReadAllText(Path.Combine(FindRepo(), "services/Autonogrammer.Api/Program.cs"));
        var services = program.IndexOf("AddDispatchServices", StringComparison.Ordinal);
        var automation = program.IndexOf("AddAutomationServices", StringComparison.Ordinal);
        services.ShouldBeGreaterThan(automation);
        program.IndexOf("DispatchDisabledMiddleware", StringComparison.Ordinal).ShouldBeGreaterThan(program.IndexOf("AutomationDisabledMiddleware", StringComparison.Ordinal));
        program.IndexOf("MapDispatchEndpoints", StringComparison.Ordinal).ShouldBeGreaterThan(program.IndexOf("MapAutomationEndpoints", StringComparison.Ordinal));
        program.ShouldNotContain("UseWebSockets", Case.Sensitive);
        program.ShouldNotContain("AddDispatchHub", Case.Sensitive);
    }

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "services/Autonogrammer.Api/Program.cs")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("services/Autonogrammer.Api/Program.cs");
    }
}
