using System.Net;
using System.Text;
using Autonogrammer.Api.Cloud;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Api.IntegrationTests.Cloud;
using Autonogrammer.Api.Operations;
using Autonogrammer.Api.Services;
using Autonogrammer.Sandbox;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Operations;

/// <summary>
/// The Railway leg of <c>erase-account</c>. Discovery lists the project's services over the verified <c>projectServices</c> query and keeps the platform
/// workers (the name prefix). The account's folder is purged on every discovered worker; a discovery failure or any purge failure fails erasure, names the
/// host and never the token.
/// </summary>
public sealed class RailwayWorkerPurgeTests
{
    private const string Token = "railway-test-token-not-for-logs";
    private const string Owner = "alice";
    private const string Alice = "autonogrammer-agent-alice";
    private const string Bob = "autonogrammer-agent-bob";
    private const string Carol = "autonogrammer-agent-carol";
    private const string ServicesJson = """{"data":{"project":{"services":{"edges":[{"node":{"id":"svc-api","name":"autonogrammer-api"}},{"node":{"id":"svc-alice","name":"autonogrammer-agent-alice"}},{"node":null},{"node":{"id":"svc-bob","name":"autonogrammer-agent-bob"}}]}}}}""";

    /// <summary>The discovery keeps only the services whose name carries the worker prefix, at the private address the client derives for each.</summary>
    [Fact]
    public async Task ListWorkerBaseUrisAsync_KeepsOnlyThePlatformWorkers_AtTheirPrivateAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new GraphQlHandler(HttpStatusCode.OK, ServicesJson);
        using var graphQl = new HttpClient(handler);
        using var probe = new HttpClient();
        var client = Client(graphQl, probe);

        var uris = await client.ListWorkerBaseUrisAsync(ct).ConfigureAwait(true);

        uris.Select(static uri => uri.AbsoluteUri).ToArray().ShouldBe([WorkerUrl(Alice), WorkerUrl(Bob)]);
        handler.Authorization.ShouldBe("Bearer " + Token);
        (handler.Document ?? string.Empty).ShouldContain("projectServices");
    }

    /// <summary>A project with no services has no workers, which is an empty discovery, not a failure.</summary>
    [Fact]
    public async Task ListWorkerBaseUrisAsync_NoServices_IsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new GraphQlHandler(HttpStatusCode.OK, """{"data":{"project":{"services":{"edges":[]}}}}""");
        using var graphQl = new HttpClient(handler);
        using var probe = new HttpClient();
        var client = Client(graphQl, probe);

        var uris = await client.ListWorkerBaseUrisAsync(ct).ConfigureAwait(true);

        uris.ShouldBeEmpty();
    }

    /// <summary>A server error on the listing is a transport failure of the discovery, and the token is not in its message.</summary>
    [Fact]
    public async Task ListWorkerBaseUrisAsync_ServerError_ThrowsATransportFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new GraphQlHandler(HttpStatusCode.InternalServerError, "{}");
        using var graphQl = new HttpClient(handler);
        using var probe = new HttpClient();
        var client = Client(graphQl, probe);

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(() => client.ListWorkerBaseUrisAsync(ct)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.Transport);
        failure.Message.ShouldNotContain(Token);
    }

    /// <summary>Every discovered worker is purged with the account's folder name; a worker that answers 500 fails erasure, every other worker is still tried, and the failing host is named without a token.</summary>
    [Fact]
    public async Task PurgeRailwayWorkers_OneWorkerReturns500_FailsClosedAndNamesTheHost()
    {
        var ct = TestContext.Current.CancellationToken;
        using var keys = new CloudTestKeys();
        using var issuer = NewIssuer(keys);
        using var handler = new PurgeHandler(failingHost: Bob + ".railway.internal:8080");
        using var http = new HttpClient(handler);
        using var error = new StringWriter();
        Uri[] workers = [new(WorkerUrl(Alice)), new(WorkerUrl(Bob)), new(WorkerUrl(Carol))];

        var purged = await EraseAccountCommand.PurgeRailwayWorkersAsync(
            _ => Task.FromResult<IReadOnlyList<Uri>>(workers),
            new WorkerPurgeClient(http, issuer),
            AccountWorkspace.DirectoryName(Owner),
            error,
            ct).ConfigureAwait(true);

        purged.ShouldBeNull();
        handler.Hosts.ToArray().ShouldBe([Alice + ".railway.internal:8080", Bob + ".railway.internal:8080", Carol + ".railway.internal:8080"]);
        error.ToString().ShouldContain(Bob + ".railway.internal:8080");
        error.ToString().ShouldNotContain("Bearer");
        error.ToString().ShouldNotContain("eyJ");
    }

    /// <summary>When every discovered worker answers 204, the purge succeeds and reports the number of workers purged.</summary>
    [Fact]
    public async Task PurgeRailwayWorkers_EveryWorkerPurged_ReturnsTheCount()
    {
        var ct = TestContext.Current.CancellationToken;
        using var keys = new CloudTestKeys();
        using var issuer = NewIssuer(keys);
        using var handler = new PurgeHandler(failingHost: string.Empty);
        using var http = new HttpClient(handler);
        using var error = new StringWriter();
        Uri[] workers = [new(WorkerUrl(Alice)), new(WorkerUrl(Bob))];

        var purged = await EraseAccountCommand.PurgeRailwayWorkersAsync(
            _ => Task.FromResult<IReadOnlyList<Uri>>(workers),
            new WorkerPurgeClient(http, issuer),
            AccountWorkspace.DirectoryName(Owner),
            error,
            ct).ConfigureAwait(true);

        purged.ShouldBe(2);
        handler.Hosts.Count.ShouldBe(2);
        error.ToString().ShouldBeEmpty();
    }

    /// <summary>Zero discovered workers is a success with nothing sent.</summary>
    [Fact]
    public async Task PurgeRailwayWorkers_NoWorkers_SucceedsWithoutSending()
    {
        var ct = TestContext.Current.CancellationToken;
        using var keys = new CloudTestKeys();
        using var issuer = NewIssuer(keys);
        using var handler = new PurgeHandler(failingHost: string.Empty);
        using var http = new HttpClient(handler);
        using var error = new StringWriter();

        var purged = await EraseAccountCommand.PurgeRailwayWorkersAsync(
            _ => Task.FromResult<IReadOnlyList<Uri>>([]),
            new WorkerPurgeClient(http, issuer),
            AccountWorkspace.DirectoryName(Owner),
            error,
            ct).ConfigureAwait(true);

        purged.ShouldBe(0);
        handler.Hosts.ShouldBeEmpty();
    }

    /// <summary>A failed discovery fails erasure before any purge is sent, and names the failure kind only.</summary>
    [Fact]
    public async Task PurgeRailwayWorkers_DiscoveryFails_FailsClosedBeforeSending()
    {
        var ct = TestContext.Current.CancellationToken;
        using var keys = new CloudTestKeys();
        using var issuer = NewIssuer(keys);
        using var handler = new PurgeHandler(failingHost: string.Empty);
        using var http = new HttpClient(handler);
        using var error = new StringWriter();

        var purged = await EraseAccountCommand.PurgeRailwayWorkersAsync(
            _ => Task.FromException<IReadOnlyList<Uri>>(new CloudEnvironmentException("Railway projectServices failed.", CloudEnvironmentFailure.Transport)),
            new WorkerPurgeClient(http, issuer),
            AccountWorkspace.DirectoryName(Owner),
            error,
            ct).ConfigureAwait(true);

        purged.ShouldBeNull();
        handler.Hosts.ShouldBeEmpty();
        error.ToString().ShouldContain("Transport");
    }

    private static string WorkerUrl(string serviceName) => $"http://{serviceName}.railway.internal:8080/";

    private static WorkerTokenIssuer NewIssuer(CloudTestKeys keys) =>
        new(Microsoft.Extensions.Options.Options.Create(new CloudOptions { Worker = { SigningKeyPem = keys.SigningKeyPem } }), TimeProvider.System);

    private static RailwayCloudEnvironmentClient Client(HttpClient graphQl, HttpClient probe) =>
        new(
            graphQl,
            probe,
            new RailwayCloudEnvironmentOptions { ApiToken = Token, ProjectId = "project-1", EnvironmentId = "environment-1", Image = "ghcr.io/example/worker:1" },
            TimeProvider.System);

    /// <summary>Answers every GraphQL request with one status and body, and keeps the authorization and the document it received.</summary>
    private sealed class GraphQlHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        public string? Document { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Document = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    /// <summary>Answers the purge of one host with 500 and every other host with 204, and keeps the authority of each request in order.</summary>
    private sealed class PurgeHandler(string failingHost) : HttpMessageHandler
    {
        public List<string> Hosts { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authority = request.RequestUri!.Authority;
            Hosts.Add(authority);
            var status = string.Equals(authority, failingHost, StringComparison.Ordinal) ? HttpStatusCode.InternalServerError : HttpStatusCode.NoContent;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
