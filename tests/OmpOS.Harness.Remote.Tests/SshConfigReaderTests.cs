using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>Listing the concrete host aliases of <c>~/.ssh/config</c> and its includes for the host picker.</summary>
public sealed class SshConfigReaderTests
{
    private static string WriteFile(TempDirectory home, string relative, string content)
    {
        var path = home.Combine(relative.Split('/'));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Concrete aliases are listed once, in file order, across relative and home-relative includes; patterns, negations and Match blocks are skipped.</summary>
    [Fact]
    public void AliasesAreListedAcrossIncludes()
    {
        using var home = TempDirectory.Create();
        var main = WriteFile(
            home,
            ".ssh/config",
            """
            # personal hosts
            Host alpha beta
              HostName a.example
            Host *.internal
              User ops
            Host !blocked gamma
            Include conf.d/*.conf
            Include ~/extra/other
            Match originalhost delta
              User matched
            Host "quoted one"
            Host epsilon # trailing comment
            Host=kappa
            host lambda
            """);
        _ = WriteFile(home, ".ssh/conf.d/10-a.conf", "Host zeta\n  HostName z.example\n");
        _ = WriteFile(home, ".ssh/conf.d/20-b.conf", "Host eta alpha\n");
        _ = WriteFile(home, ".ssh/conf.d/ignored.txt", "Host not-included\n");
        _ = WriteFile(home, "extra/other", "Host theta\n");

        var listing = new SshConfigReader(home.Path).ListHosts(main);

        listing.Hosts.Select(host => host.Alias).ToArray().ShouldBe(["alpha", "beta", "gamma", "zeta", "eta", "theta", "epsilon", "kappa", "lambda"]);
        listing.Hosts.Single(host => string.Equals(host.Alias, "alpha", StringComparison.Ordinal)).Line.ShouldBe(2);
        listing.Hosts.Single(host => string.Equals(host.Alias, "alpha", StringComparison.Ordinal)).SourceFile.ShouldBe(main);
        listing.Hosts.Single(host => string.Equals(host.Alias, "zeta", StringComparison.Ordinal)).SourceFile.ShouldEndWith("10-a.conf", Case.Sensitive);
        listing.Warnings.Count.ShouldBe(1);
        listing.Warnings[0].ShouldContain("cannot be used as an OmpOS alias", Case.Sensitive);
    }

    /// <summary>A missing configuration file is an empty listing, and the default path is under the home directory.</summary>
    [Fact]
    public void AMissingConfigIsEmpty()
    {
        using var home = TempDirectory.Create();
        var reader = new SshConfigReader(home.Path);

        reader.DefaultConfigPath.ShouldBe(Path.Combine(home.Path, ".ssh", "config"));
        reader.ListHosts().Hosts.ShouldBeEmpty();
        reader.ListHosts().Warnings.ShouldBeEmpty();
    }

    /// <summary>An include that points back at its own file is read once, and a host whose name cannot be an alias is reported, not listed.</summary>
    [Fact]
    public void IncludeLoopsAreBroken()
    {
        using var home = TempDirectory.Create();
        var main = WriteFile(home, ".ssh/config", "Include config\nHost solo\n");

        var listing = new SshConfigReader(home.Path).ListHosts(main);

        listing.Hosts.Select(host => host.Alias).ToArray().ShouldBe(["solo"]);
    }

    /// <summary>A chain of includes deeper than the limit stops with a warning instead of recursing without end.</summary>
    [Fact]
    public void ADeepIncludeChainStopsWithAWarning()
    {
        using var home = TempDirectory.Create();
        var main = WriteFile(home, ".ssh/config", "Include chain/0\nHost root-host\n");
        for (var index = 0; index < SshConfigReader.MaxIncludeDepth + 4; index++)
        {
            _ = WriteFile(home, ".ssh/chain/" + index, $"Host hop{index}\nInclude chain/{index + 1}\n");
        }

        var listing = new SshConfigReader(home.Path).ListHosts(main);

        listing.Warnings.ShouldContain(warning => warning.Contains("deeper than", StringComparison.Ordinal));
        listing.Hosts.Count.ShouldBeLessThan(SshConfigReader.MaxIncludeDepth + 4);
    }

    /// <summary>A wildcard in a directory name of an include is not followed, and says so.</summary>
    [Fact]
    public void AWildcardDirectoryIncludeIsReported()
    {
        using var home = TempDirectory.Create();
        var main = WriteFile(home, ".ssh/config", "Include */extra.conf\nHost kept\n");

        var listing = new SshConfigReader(home.Path).ListHosts(main);

        listing.Hosts.Select(host => host.Alias).ToArray().ShouldBe(["kept"]);
        listing.Warnings.ShouldContain(warning => warning.Contains("wildcard in a directory", StringComparison.Ordinal));
    }
}
