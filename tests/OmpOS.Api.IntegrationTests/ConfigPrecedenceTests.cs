using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// OPS-01 layered configuration. An environment variable overrides the appsettings value of a bound option, and a Production host
/// that lacks a required option refuses to start, naming the key.
/// </summary>
public sealed class ConfigPrecedenceTests
{
    /// <summary>
    /// The prefix scoping this test's environment variable. Process environment variables are shared by every host in the test run,
    /// so the override uses a name no other host reads and cannot race them.
    /// </summary>
    private const string EnvironmentPrefix = "OMPOS_CONFIG_PRECEDENCE_TEST_";

    /// <summary>Verifies an environment variable overrides the appsettings value of Api:MaxPromptLength, and the appsettings value holds without it.</summary>
    [Fact]
    public void EnvironmentVariable_OverridesAppsettingsForOneBoundOption()
    {
        using var factory = new OmpApiFactory();
        factory.Services.GetRequiredService<IOptions<ApiOptions>>().Value.MaxPromptLength.ShouldBe(64000, "the default host binds the appsettings value");

        var appsettings = Path.Combine(FindRepoRoot(), "services", "OmpOS.Api", "appsettings.json");
        var variable = EnvironmentPrefix + "Api__MaxPromptLength";
        BindMaxPromptLength(appsettings).ShouldBe(64000, "with no environment override the appsettings value is bound");

        Environment.SetEnvironmentVariable(variable, "12000");
        try
        {
            BindMaxPromptLength(appsettings).ShouldBe(12000, "the environment variable overrides the appsettings value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    /// <summary>Verifies a Production host without Harness:Adapter refuses to start, and the startup failure names the key.</summary>
    [Fact]
    public void ProductionWithoutHarnessAdapter_FailsStartupNamingTheKey()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Api:Auth:Authority"] = JwtTestHost.Issuer,
            ["Api:Auth:Audience"] = JwtTestHost.Audience,
            ["Api:Auth:ValidIssuer"] = JwtTestHost.Issuer,
            ["Harness:Adapter"] = string.Empty,
        };
        using var factory = new OmpApiFactory(config, environment: "Production");

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("Harness:Adapter");
    }

    /// <summary>Builds the configuration the host builds from appsettings.json and then environment variables, and binds Api:MaxPromptLength.</summary>
    /// <param name="appsettingsPath">The path of the API appsettings file.</param>
    /// <returns>The bound MaxPromptLength.</returns>
    private static int BindMaxPromptLength(string appsettingsPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(appsettingsPath, optional: false)
            .AddEnvironmentVariables(EnvironmentPrefix)
            .Build();
        return configuration.GetSection(ApiOptions.SectionName).Get<ApiOptions>()?.MaxPromptLength
            ?? throw new InvalidOperationException("The Api section did not bind.");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Could not locate OmpOS.slnx walking up from " + AppContext.BaseDirectory)
            : directory.FullName;
    }
}
