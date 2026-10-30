using System.Net;
using System.Text;
using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Fail-closed behaviour of the agentOS and Railway providers when a per-sandbox credential is requested:
/// provisioning throws <see cref="NotAvailableException"/> before any request is sent. Without the flag, and for
/// teardown, behaviour is unchanged. The podman credential path is covered by PodmanSandboxCredentialTests and
/// PerSandboxCredentialFlagTests.
/// </summary>
public sealed class NonPodmanCredentialFailClosedTests
{
    private const string SentinelCredential = "autonogrammer-sentinel-credential-7f3a9c";
    private const string AgentOsToken = "sk_test_secret";
    private const string AgentOsNamespace = "ns-test";
    private const string RailwayToken = "railway-test-token";
    private const string CreatedVmJson = """{"actor":{"actor_id":"vm-1"},"created":true}""";
    private const string NullOutputJson = """{"output":null}""";
    private const string ServiceDeletedJson = """{"data":{"serviceDelete":true}}""";

    [Fact]
    public async Task AgentOs_PerSandboxCredentialRequested_ThrowsNotAvailableWithoutSendingRequest()
    {
        using var handler = new RecordingHandler(AgentOsRespond);
        using var http = new HttpClient(handler);
        var provider = new AgentOsProvider(http, new Uri("https://api.rivet.dev"), AgentOsNamespace, AgentOsToken, new SandboxProviderOptions());

        var exception = await Should.ThrowAsync<NotAvailableException>(
            () => provider.ProvisionAsync(CreateRequest(supplyCredential: true), TestContext.Current.CancellationToken));

        handler.Requests.ShouldBeEmpty();
        exception.Message.ShouldContain("per-sandbox credential");
        exception.Message.ShouldContain("secret");
        exception.Message.ShouldContain("provisioning");
        exception.Message.ShouldNotContain(SentinelCredential);
    }

    [Fact]
    public async Task AgentOs_NoCredentialOptions_SendsFirstRequestAsBefore()
    {
        using var handler = new RecordingHandler(AgentOsRespond);
        using var http = new HttpClient(handler);
        var provider = new AgentOsProvider(http, new Uri("https://api.rivet.dev"), AgentOsNamespace, AgentOsToken);

        var result = await provider.ProvisionAsync(CreateRequest(supplyCredential: false), TestContext.Current.CancellationToken);

        var first = handler.Requests[0];
        first.Method.ShouldBe(HttpMethod.Put);
        first.Target.ShouldBe("/actors?namespace=ns-test");
        result.Descriptor.Id.ShouldBe("vm-1");
    }

    [Fact]
    public async Task AgentOs_PerSandboxCredentialsDisabled_SendsFirstRequestAsBefore()
    {
        using var handler = new RecordingHandler(AgentOsRespond);
        using var http = new HttpClient(handler);
        var options = new SandboxProviderOptions { PerSandboxCredentials = false };
        var provider = new AgentOsProvider(http, new Uri("https://api.rivet.dev"), AgentOsNamespace, AgentOsToken, options);

        var result = await provider.ProvisionAsync(CreateRequest(supplyCredential: false), TestContext.Current.CancellationToken);

        var first = handler.Requests[0];
        first.Method.ShouldBe(HttpMethod.Put);
        first.Target.ShouldBe("/actors?namespace=ns-test");
        result.Descriptor.Id.ShouldBe("vm-1");
    }

    [Fact]
    public async Task AgentOs_PerSandboxCredentialRequested_TeardownStillDeletesActor()
    {
        using var handler = new RecordingHandler(AgentOsRespond);
        using var http = new HttpClient(handler);
        var provider = new AgentOsProvider(http, new Uri("https://api.rivet.dev"), AgentOsNamespace, AgentOsToken, new SandboxProviderOptions());

        await provider.TeardownAsync("vm-1", TestContext.Current.CancellationToken);

        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Delete);
        sent.Target.ShouldBe("/actors/vm-1?namespace=ns-test");
    }

    [Fact]
    public async Task Railway_PerSandboxCredentialRequested_ThrowsNotAvailableWithoutSendingRequest()
    {
        using var handler = new RecordingHandler(_ => Json(ServiceDeletedJson));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, RailwayToken, new SandboxProviderOptions());

        var exception = await Should.ThrowAsync<NotAvailableException>(
            () => provider.ProvisionAsync(CreateRequest(supplyCredential: true), TestContext.Current.CancellationToken));

        handler.Requests.ShouldBeEmpty();
        exception.Message.ShouldContain("per-sandbox credential");
        exception.Message.ShouldContain("secret");
        exception.Message.ShouldContain("SandboxCreateInput");
        exception.Message.ShouldNotContain(SentinelCredential);
    }

    [Fact]
    public async Task Railway_NoCredentialOptions_ProvisionRefusalUnchangedWithoutSendingRequest()
    {
        using var handler = new RecordingHandler(_ => Json(ServiceDeletedJson));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, RailwayToken);

        var exception = await Should.ThrowAsync<NotAvailableException>(
            () => provider.ProvisionAsync(CreateRequest(supplyCredential: false), TestContext.Current.CancellationToken));

        handler.Requests.ShouldBeEmpty();
        exception.Message.ShouldContain("sandboxCreate");
        exception.Message.ShouldNotContain("per-sandbox credential");
    }

    [Fact]
    public async Task Railway_PerSandboxCredentialRequested_TeardownStillDeletesService()
    {
        using var handler = new RecordingHandler(_ => Json(ServiceDeletedJson));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, RailwayToken, new SandboxProviderOptions());

        await provider.TeardownAsync("svc-1", TestContext.Current.CancellationToken);

        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Post);
        sent.Target.ShouldBe("/graphql/v2");
        sent.Body.ShouldContain("svc-1");
    }

    private static HttpResponseMessage AgentOsRespond(SentRequest sent) => sent.Method.Equals(HttpMethod.Delete)
        ? new HttpResponseMessage(HttpStatusCode.OK)
        : Json(sent.Target.StartsWith("/actors?", StringComparison.Ordinal) ? CreatedVmJson : NullOutputJson);

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static ProvisionRequest CreateRequest(bool supplyCredential)
    {
        var environment = supplyCredential
            ? new Dictionary<string, string>(StringComparer.Ordinal) { ["AUTONOGRAMMER_DEBUG"] = SentinelCredential }
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var secretRefs = supplyCredential
            ? new[] { new SecretRef { Key = "credential", ProviderSecretId = SentinelCredential, TargetEnvVar = "AUTONOGRAMMER_CREDENTIAL" } }
            : Array.Empty<SecretRef>();
        return new ProvisionRequest
        {
            IdempotencyKey = "autonogrammer-session-key",
            SessionId = "session-1",
            Timeout = TimeSpan.FromSeconds(30),
            Isolation = Isolation.Process,
            NetworkPolicy = NetworkPolicy.DefaultDeny,
            Mounts = Array.Empty<MountSpec>(),
            SecretRefs = secretRefs,
            Spec = new SandboxSpec
            {
                Image = string.Empty,
                Command = Array.Empty<string>(),
                WorkingDirectory = string.Empty,
                Environment = environment,
                Limits = null,
            },
        };
    }

    private sealed class RecordingHandler(Func<SentRequest, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var sent = new SentRequest(request.Method, request.RequestUri?.PathAndQuery ?? string.Empty, body);
            Requests.Add(sent);
            return respond(sent);
        }
    }

    private sealed record SentRequest(HttpMethod Method, string Target, string Body);
}
