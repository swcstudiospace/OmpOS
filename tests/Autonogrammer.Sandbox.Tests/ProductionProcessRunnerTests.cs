using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Verifies the sandbox registration resolves the production <see cref="ProcessRunner"/> and that
/// it passes argv elements through unchanged (TD-25, audit WR-09).
/// </summary>
public sealed class ProductionProcessRunnerTests
{
    /// <summary>
    /// Resolving <see cref="IProcessRunner"/> from a provider built with the sandbox registration returns a <see cref="ProcessRunner"/>.
    /// </summary>
    [Fact]
    public void SandboxRegistration_ResolvesProductionProcessRunner()
    {
        using var provider = new ServiceCollection()
            .AddSandboxRunner()
            .BuildServiceProvider();

        var runner = provider.GetRequiredService<IProcessRunner>();

        runner.ShouldBeOfType<ProcessRunner>();
    }

    /// <summary>
    /// A runner the host registered before the sandbox registration is kept; the sandbox registration does not replace it.
    /// </summary>
    [Fact]
    public void SandboxRegistration_KeepsRunnerAlreadyRegisteredByHost()
    {
        var hostRunner = new ProcessRunner();
        using var provider = new ServiceCollection()
            .AddSingleton<IProcessRunner>(hostRunner)
            .AddSandboxRunner()
            .BuildServiceProvider();

        var runner = provider.GetRequiredService<IProcessRunner>();

        runner.ShouldBeSameAs(hostRunner);
    }

    /// <summary>
    /// An argv element containing spaces, quotes and a semicolon reaches the child as one argument and is echoed back exactly.
    /// </summary>
    [Fact]
    public async Task ArgvValueWithSpacesQuotesAndSemicolon_IsReturnedVerbatimOnStdout()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "printf argv test requires Linux.");
        Assert.SkipUnless(File.Exists("/bin/printf"), "/bin/printf not available.");
        var ct = TestContext.Current.CancellationToken;
        const string value = "hello world; \"quoted\" 'single' && tail";

        using var provider = new ServiceCollection()
            .AddSandboxRunner()
            .BuildServiceProvider();
        var runner = provider.GetRequiredService<IProcessRunner>();

        var result = await runner.RunAsync(new ProcessSpec
        {
            Argv = ["/bin/printf", "%s", value],
            WorkingDirectory = "/",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Timeout = TimeSpan.FromSeconds(30),
        }, ct);

        result.ExitCode.ShouldBe(0);
        result.Stdout.ShouldBe(value);
    }
}
