using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Tests for the constructor validation and defaults of <see cref="RailwayCloudEnvironmentClient"/> and its options.
/// </summary>
public sealed class RailwayOptionsValidationTests
{
    [Fact]
    public void DefaultsMatchTheDocumentedValues()
    {
        var options = new RailwayCloudEnvironmentOptions();

        options.Endpoint.ShouldBe(new Uri("https://backboard.railway.com/graphql/v2"));
        options.ServiceNamePrefix.ShouldBe("ompos-agent-");
        options.VolumeMountPath.ShouldBe("/data");
        options.HealthPath.ShouldBe("/healthz");
        options.WorkerPort.ShouldBe(8080);
        options.WorkerHostSuffix.ShouldBe(".railway.internal");
        options.WakeTimeout.ShouldBe(TimeSpan.FromSeconds(60));
        options.WakeRetryInterval.ShouldBe(TimeSpan.FromSeconds(2));
        options.EnableServerless.ShouldBeTrue();
        options.Variables.ShouldBeEmpty();
        options.SecretVariablesProvider.ShouldBeNull();
        new StaticPoolEnvironmentOptions().WakeTimeout.ShouldBe(TimeSpan.FromSeconds(60));
        new StaticPoolEnvironmentOptions().WakeRetryInterval.ShouldBe(TimeSpan.FromSeconds(2));
        new StaticPoolEnvironmentOptions().HealthPath.ShouldBe("/healthz");
    }

    [Fact]
    public void ValidOptionsAreAccepted()
    {
        using var api = new HttpClient();
        using var probe = new HttpClient();

        var client = new RailwayCloudEnvironmentClient(api, probe, RailwayHarness.DefaultOptions(), TimeProvider.System);

        client.ShouldNotBeNull();
    }

    [Fact]
    public void InvalidOptionsAreRejected()
    {
        AssertRejected(options => options.ApiToken = " ");
        AssertRejected(options => options.ProjectId = string.Empty);
        AssertRejected(options => options.EnvironmentId = string.Empty);
        AssertRejected(options => options.Image = string.Empty);
        AssertRejected(options => options.Endpoint = new Uri("/graphql", UriKind.Relative));
        AssertRejected(options => options.Endpoint = new Uri("ftp://backboard.railway.com/graphql/v2"));
        AssertRejected(options => options.ServiceNamePrefix = string.Empty);
        AssertRejected(options => options.ServiceNamePrefix = "Upper-");
        AssertRejected(options => options.ServiceNamePrefix = "has space");
        AssertRejected(options => options.ServiceNamePrefix = new string('a', RailwayServiceLabel.MaxPrefixLength + 1));
        AssertRejected(options => options.VolumeMountPath = "data");
        AssertRejected(options => options.HealthPath = "healthz");
        AssertRejected(options => options.HealthcheckPath = "ready");
        AssertRejected(options => options.WorkerPort = 0);
        AssertRejected(options => options.WorkerPort = 65536);
        AssertRejected(options => options.WorkerHostSuffix = " ");
        AssertRejected(options => options.WakeTimeout = TimeSpan.Zero);
        AssertRejected(options => options.WakeRetryInterval = TimeSpan.FromSeconds(-1));
    }

    [Fact]
    public void ConstructorRejectsMissingArguments()
    {
        using var api = new HttpClient();
        using var probe = new HttpClient();
        var options = RailwayHarness.DefaultOptions();

        Should.Throw<ArgumentNullException>(() => { _ = new RailwayCloudEnvironmentClient(null!, probe, options, TimeProvider.System); });
        Should.Throw<ArgumentNullException>(() => { _ = new RailwayCloudEnvironmentClient(api, null!, options, TimeProvider.System); });
        Should.Throw<ArgumentNullException>(() => { _ = new RailwayCloudEnvironmentClient(api, probe, null!, TimeProvider.System); });
        Should.Throw<ArgumentNullException>(() => { _ = new RailwayCloudEnvironmentClient(api, probe, options, null!); });
    }

    [Fact]
    public async Task AnEmptySessionIdAfterSanitisingIsRejectedBeforeAnyRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();

        await Should.ThrowAsync<ArgumentException>(
            async () => await harness.Client.CreateContainerAsync("!!!", ct).ConfigureAwait(true)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(
            async () => await harness.Client.FindVolumeAsync(" ", ct).ConfigureAwait(true)).ConfigureAwait(true);

        harness.Api.Requests.ShouldBeEmpty();
    }

    private static void AssertRejected(Action<RailwayCloudEnvironmentOptions> mutate)
    {
        var options = RailwayHarness.DefaultOptions();
        mutate(options);
        using var api = new HttpClient();
        using var probe = new HttpClient();

        Should.Throw<ArgumentException>(() => { _ = new RailwayCloudEnvironmentClient(api, probe, options, TimeProvider.System); });
    }
}
