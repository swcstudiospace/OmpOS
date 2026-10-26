using OmpOS.Harness.AgentLoop.Gsd;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// Verifies the resolution order of the tool script: an explicit location is strict, then the environment, then the default
/// install locations, and null when nothing is installed.
/// </summary>
public sealed class GsdToolsLocatorTests
{
    [Fact]
    public void ExplicitScriptFileIsUsedAsGiven()
    {
        using var directory = new GsdTempDirectory();
        var script = directory.CreateToolsHome("explicit");

        new GsdToolsLocator(script, static _ => null, homeDirectory: null).Locate().ShouldBe(script);
    }

    [Fact]
    public void ExplicitInstallDirectoryResolvesItsBinScript()
    {
        using var directory = new GsdTempDirectory();
        var script = directory.CreateToolsHome("explicit");

        new GsdToolsLocator(directory.PathOf("explicit"), static _ => null, homeDirectory: null).Locate().ShouldBe(script);
    }

    [Fact]
    public void MissingExplicitLocationDoesNotFallBackToAnotherInstall()
    {
        using var directory = new GsdTempDirectory();
        var other = directory.CreateToolsHome("other");
        Dictionary<string, string?> environment = new(StringComparer.Ordinal) { [GsdToolsLocator.HomeVariableName] = Path.GetDirectoryName(Path.GetDirectoryName(other)) };

        new GsdToolsLocator(directory.PathOf("missing"), name => environment.GetValueOrDefault(name), homeDirectory: null)
            .Locate().ShouldBeNull();
    }

    [Fact]
    public void EnvironmentHomeBeatsKimiConfigAndDefaults()
    {
        using var directory = new GsdTempDirectory();
        var fromEnvironment = directory.CreateToolsHome("env-home");
        directory.CreateToolsHome(Path.Combine("kimi", "gsd-core"));
        directory.CreateToolsHome(Path.Combine("home", ".agents", "gsd-core"));
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            [GsdToolsLocator.HomeVariableName] = directory.PathOf("env-home"),
            [GsdToolsLocator.KimiConfigVariableName] = directory.PathOf("kimi"),
        };

        new GsdToolsLocator(null, name => environment.GetValueOrDefault(name), directory.PathOf("home"))
            .Locate().ShouldBe(fromEnvironment);
    }

    [Fact]
    public void KimiConfigBeatsTheHomeDefaultsAndConfigAgentsBeatsDotAgents()
    {
        using var directory = new GsdTempDirectory();
        var kimi = directory.CreateToolsHome(Path.Combine("kimi", "gsd-core"));
        var configAgents = directory.CreateToolsHome(Path.Combine("home", ".config", "agents", "gsd-core"));
        directory.CreateToolsHome(Path.Combine("home", ".agents", "gsd-core"));
        Dictionary<string, string?> environment = new(StringComparer.Ordinal) { [GsdToolsLocator.KimiConfigVariableName] = directory.PathOf("kimi") };

        new GsdToolsLocator(null, name => environment.GetValueOrDefault(name), directory.PathOf("home")).Locate().ShouldBe(kimi);
        new GsdToolsLocator(null, static _ => null, directory.PathOf("home")).Locate().ShouldBe(configAgents);
    }

    [Fact]
    public void DotAgentsInstallIsFoundWhenNothingElseIsSet()
    {
        using var directory = new GsdTempDirectory();
        var script = directory.CreateToolsHome(Path.Combine("home", ".agents", "gsd-core"));

        new GsdToolsLocator(null, static _ => null, directory.PathOf("home")).Locate().ShouldBe(script);
    }

    [Fact]
    public void NothingInstalledYieldsNull()
    {
        using var directory = new GsdTempDirectory();
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            [GsdToolsLocator.HomeVariableName] = directory.PathOf("absent"),
            [GsdToolsLocator.KimiConfigVariableName] = "   ",
        };

        new GsdToolsLocator(null, name => environment.GetValueOrDefault(name), directory.PathOf("home")).Locate().ShouldBeNull();
    }
}
