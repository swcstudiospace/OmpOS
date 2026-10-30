using Autonogrammer.Api.Services.WorkspaceSettings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary>What <see cref="UpstreamHost"/> keeps of a configured address: the host and a non-default port, never a credential.</summary>
public sealed class UpstreamHostTests
{
    /// <summary>A URL keeps its host, and its port only when the scheme does not default to it.</summary>
    [Theory]
    [InlineData("https://login.example.com/realms/x?a=b", "login.example.com")]
    [InlineData("https://login.example.com:8443/realms/x", "login.example.com:8443")]
    [InlineData("https://user:pass@login.example.com/", "login.example.com")]
    [InlineData("http://localhost:5000", "localhost:5000")]
    public void FromUrl_KeepsTheHost(string address, string expected)
    {
        UpstreamHost.FromUrl(address).ShouldBe(expected);
    }

    /// <summary>A value that is not an absolute URL has no host.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    public void FromUrl_RefusesWhatIsNotAnAbsoluteUrl(string address)
    {
        UpstreamHost.FromUrl(address).ShouldBeNull();
    }

    /// <summary>A connection string keeps its host and port and drops the password, the user and the database.</summary>
    [Theory]
    [InlineData("Host=db.internal;Port=5433;Username=u;Password=hunter2;Database=autonogrammer", "db.internal:5433")]
    [InlineData("Server=db.internal;Password=hunter2", "db.internal")]
    [InlineData("host=db.internal:6000;password=hunter2", "db.internal:6000")]
    public void FromConnectionString_KeepsTheHostOnly(string connectionString, string expected)
    {
        var host = UpstreamHost.FromConnectionString(connectionString);

        host.ShouldBe(expected);
        host.ShouldNotBeNull().ShouldNotContain("hunter2", Case.Sensitive);
    }

    /// <summary>A connection string without a host, or one that cannot be parsed, has no host.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("Database=autonogrammer;Password=hunter2")]
    [InlineData("this is not a connection string")]
    public void FromConnectionString_RefusesWhatHasNoHost(string connectionString)
    {
        UpstreamHost.FromConnectionString(connectionString).ShouldBeNull();
    }

    /// <summary>A Redis style configuration keeps its first endpoint and drops its options.</summary>
    [Theory]
    [InlineData("dragonfly.internal:6379,password=hunter2,ssl=true", "dragonfly.internal:6379")]
    [InlineData("dragonfly.internal", "dragonfly.internal")]
    [InlineData("user@dragonfly.internal:6379", "dragonfly.internal:6379")]
    public void FromRedisConfiguration_KeepsTheFirstEndpoint(string configuration, string expected)
    {
        var host = UpstreamHost.FromRedisConfiguration(configuration);

        host.ShouldBe(expected);
        host.ShouldNotBeNull().ShouldNotContain("hunter2", Case.Sensitive);
    }

    /// <summary>A configuration that starts with an option has no endpoint to show.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("password=hunter2,dragonfly.internal:6379")]
    public void FromRedisConfiguration_RefusesAnOptionFirst(string configuration)
    {
        UpstreamHost.FromRedisConfiguration(configuration).ShouldBeNull();
    }
}
