using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Verifies the production sandbox service composition (WR-09).
/// </summary>
public sealed class SandboxServiceTests
{
    /// <summary>
    /// The sandbox runner registration resolves the production argv-only process runner.
    /// </summary>
    [Fact]
    public void AddSandboxRunner_ResolvesProductionProcessRunner()
    {
        var services = new ServiceCollection();
        services.AddSandboxRunner();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IProcessRunner>().ShouldBeOfType<ProcessRunner>();
    }
}
