using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Providers.Tests;

/// <summary>Probes catalogue entries against an in-process fake server and against a loopback port nothing listens on.</summary>
public sealed class LocalProviderProbeTests
{
    private const string ModelList = """{"object":"list","data":[{"id":"llama3.2","object":"model"},{"id":"qwen3-coder"},{"object":"model"}]}""";

    private static CancellationToken NoCancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProbeReportsNotRunningWhenNothingListens()
    {
        using var client = new HttpClient();
        var probe = new LocalProviderProbe(client, TimeProvider.System);
        var entry = ProviderCatalogue.OllamaLocalOpenAi with { BaseEndpoint = FakeOpenAiServer.ClosedBaseUri() };

        var result = await probe.ProbeAsync(entry, NoCancellation).ConfigureAwait(true);

        result.Status.ShouldBe(ProviderProbeStatus.NotRunning);
        result.IsReachable.ShouldBeFalse();
        result.HttpStatus.ShouldBeNull();
        result.ModelIds.ShouldBeEmpty();
        result.Message.ShouldContain("not running");
        result.Message.ShouldContain(result.ProbedUri.AbsoluteUri);
    }

    [Fact]
    public async Task ProbeDetectsAFakeOpenAiServer()
    {
        await using var server = FakeOpenAiServer.Start(ModelList);
        using var client = new HttpClient();
        var probe = new LocalProviderProbe(client, TimeProvider.System);
        var entry = ProviderCatalogue.LmStudioLocalOpenAi with { BaseEndpoint = server.BaseUri };

        var result = await probe.ProbeAsync(entry, NoCancellation).ConfigureAwait(true);

        result.Status.ShouldBe(ProviderProbeStatus.Running);
        result.HttpStatus.ShouldBe(200);
        result.ModelIds.ShouldBe(["llama3.2", "qwen3-coder"]);
        server.Paths.ShouldBe(["/v1/models"]);
        server.SawAuthorization.ShouldBeFalse();
    }

    [Fact]
    public async Task AServerThatWantsAKeyIsReportedAsRunning()
    {
        await using var server = FakeOpenAiServer.Start("""{"error":"unauthorized"}""", HttpStatusCode.Unauthorized);
        using var client = new HttpClient();
        var probe = new LocalProviderProbe(client, TimeProvider.System);
        var entry = ProviderCatalogue.VllmLocalOpenAi with { BaseEndpoint = server.BaseUri };

        var result = await probe.ProbeAsync(entry, NoCancellation).ConfigureAwait(true);

        result.Status.ShouldBe(ProviderProbeStatus.RunningAuthRequired);
        result.IsReachable.ShouldBeTrue();
        result.HttpStatus.ShouldBe(401);
        result.Message.ShouldContain("API key");
    }

    [Theory]
    [InlineData("<html>not an api</html>", HttpStatusCode.OK)]
    [InlineData("""{"models":[]}""", HttpStatusCode.OK)]
    [InlineData("""{"data":"nope"}""", HttpStatusCode.OK)]
    [InlineData("""{"error":"boom"}""", HttpStatusCode.InternalServerError)]
    public async Task ASocketThatIsNotAnOpenAiServerIsFlaggedAsUnexpected(string body, HttpStatusCode status)
    {
        await using var server = FakeOpenAiServer.Start(body, status);
        using var client = new HttpClient();
        var probe = new LocalProviderProbe(client, TimeProvider.System);
        var entry = ProviderCatalogue.JanLocalOpenAi with { BaseEndpoint = server.BaseUri };

        var result = await probe.ProbeAsync(entry, NoCancellation).ConfigureAwait(true);

        result.Status.ShouldBe(ProviderProbeStatus.UnexpectedResponse);
        result.ModelIds.ShouldBeEmpty();
        result.Message.ShouldContain(entry.Id);
    }

    [Theory]
    [InlineData("http://localhost:11434/v1/", "http://localhost:11434/v1/models")]
    [InlineData("http://127.0.0.1:9931", "http://127.0.0.1:9931/v1/models")]
    [InlineData("http://localhost:12434/engines/v1", "http://localhost:12434/engines/v1/models")]
    public void ModelsUriFollowsTheBaseEndpointShape(string baseEndpoint, string expected)
    {
        LocalProviderProbe.ModelsUri(new Uri(baseEndpoint)).AbsoluteUri.ShouldBe(expected);
    }

    [Fact]
    public async Task ProbeRefusesEntriesThatAreNotLocalOpenAiCompatible()
    {
        using var client = new HttpClient();
        var probe = new LocalProviderProbe(client, TimeProvider.System);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await probe.ProbeAsync(ProviderCatalogue.HuggingFaceRouter, NoCancellation).ConfigureAwait(true)).ConfigureAwait(true);
    }

    [Fact]
    public async Task ACancelledProbeThrowsInsteadOfClaimingTheServerIsDown()
    {
        await using var server = FakeOpenAiServer.Start(ModelList);
        using var client = new HttpClient();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);
        var probe = new LocalProviderProbe(client, TimeProvider.System);
        var entry = ProviderCatalogue.OllamaLocalOpenAi with { BaseEndpoint = server.BaseUri };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await probe.ProbeAsync(entry, cancelled.Token).ConfigureAwait(true)).ConfigureAwait(true);
    }
}
