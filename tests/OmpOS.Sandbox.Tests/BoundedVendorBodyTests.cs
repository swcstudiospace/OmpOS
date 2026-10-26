using System.Net;
using System.Text;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Vendor response bodies are read under a 4 MiB limit. An oversized body fails with the provider's vendor-error type,
/// naming the limit; a body within the limit is returned unchanged.
/// </summary>
public sealed class BoundedVendorBodyTests
{
    private const string AgentOsToken = "sk_test_bounded";
    private const string AgentOsNamespace = "ns-bounded";
    private const string RailwayToken = "railway-bounded-token";
    private const string CreatedVmJson = """{"actor":{"actor_id":"vm-1"},"created":true}""";
    private const string NullOutputJson = """{"output":null}""";
    private const string ServiceDeletedJson = """{"data":{"serviceDelete":true}}""";

    private const long Limit = BoundedContent.MaxVendorBodyBytes;

    /// <summary>A declared Content-Length over the limit is refused before the body is read.</summary>
    [Fact]
    public async Task DeclaredLengthOverTheLimitIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var content = new ByteArrayContent(new byte[Limit + 1]);

        var ex = await Should.ThrowAsync<InvalidDataException>(() => BoundedContent.ReadTextAsync(content, Limit, ct));

        ex.Message.ShouldContain("over the");
    }

    /// <summary>A streamed body with no declared length is refused as soon as it passes the limit.</summary>
    [Fact]
    public async Task StreamedBodyOverTheLimitIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var content = new StreamContent(new MemoryStream(new byte[Limit + 1]));

        var ex = await Should.ThrowAsync<InvalidDataException>(() => BoundedContent.ReadTextAsync(content, Limit, ct));

        ex.Message.ShouldContain("4194304-byte limit");
    }

    /// <summary>A body within the limit, including one of exactly the limit, is returned unchanged.</summary>
    [Fact]
    public async Task BodyWithinTheLimitIsReturnedUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var small = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json");
        (await BoundedContent.ReadTextAsync(small, Limit, ct)).ShouldBe("{\"ok\":true}");

        using var exact = new ByteArrayContent(Encoding.UTF8.GetBytes(new string('e', (int)Limit)));
        (await BoundedContent.ReadTextAsync(exact, Limit, ct)).Length.ShouldBe((int)Limit);
    }

    /// <summary>An oversized agentOS response fails provisioning as NotAvailableException, naming the 4 MiB limit.</summary>
    [Fact]
    public async Task AgentOsOversizedResponseFailsAsNotAvailable()
    {
        using var handler = new FixedHandler(_ => Json(new string('x', (int)Limit + 1)));
        using var http = new HttpClient(handler);
        var provider = new AgentOsProvider(http, new Uri("https://api.rivet.dev"), AgentOsNamespace, AgentOsToken, new SandboxProviderOptions { PerSandboxCredentials = false });

        var ex = await Should.ThrowAsync<NotAvailableException>(
            () => provider.ProvisionAsync(CreateAgentOsRequest(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("4 MiB");
    }

    /// <summary>A normal agentOS response is read unchanged and provisioning returns the created actor.</summary>
    [Fact]
    public async Task AgentOsNormalResponseIsReadUnchanged()
    {
        using var handler = new FixedHandler(request => Json(request.RequestUri!.PathAndQuery.StartsWith("/actors", StringComparison.Ordinal)
            ? CreatedVmJson
            : NullOutputJson));
        using var http = new HttpClient(handler);
        var provider = new AgentOsProvider(http, new Uri("https://api.rivet.dev"), AgentOsNamespace, AgentOsToken, new SandboxProviderOptions { PerSandboxCredentials = false });

        var result = await provider.ProvisionAsync(CreateAgentOsRequest(), TestContext.Current.CancellationToken);

        result.Descriptor.Id.ShouldBe("vm-1");
    }

    /// <summary>An oversized Railway serviceDelete response fails teardown as TeardownFailedException, naming the 4 MiB limit.</summary>
    [Fact]
    public async Task RailwayOversizedResponseFailsAsTeardownFailed()
    {
        using var handler = new FixedHandler(_ => Json(new string('x', (int)Limit + 1)));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, RailwayToken, new SandboxProviderOptions());

        var ex = await Should.ThrowAsync<TeardownFailedException>(
            () => provider.TeardownAsync("svc-bounded", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("4 MiB");
    }

    /// <summary>A normal Railway serviceDelete response is read unchanged and teardown confirms the deletion.</summary>
    [Fact]
    public async Task RailwayNormalResponseIsReadUnchanged()
    {
        using var handler = new FixedHandler(_ => Json(ServiceDeletedJson));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, RailwayToken, new SandboxProviderOptions());

        await provider.TeardownAsync("svc-bounded", TestContext.Current.CancellationToken);

        handler.Requests.Count.ShouldBe(1);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static ProvisionRequest CreateAgentOsRequest() => new()
    {
        IdempotencyKey = "ompos-bounded-key",
        SessionId = "session-bounded",
        Timeout = TimeSpan.FromSeconds(30),
        Isolation = Isolation.Process,
        NetworkPolicy = NetworkPolicy.DefaultDeny,
        Mounts = Array.Empty<MountSpec>(),
        SecretRefs = Array.Empty<SecretRef>(),
        Spec = new SandboxSpec
        {
            Image = string.Empty,
            Command = Array.Empty<string>(),
            WorkingDirectory = string.Empty,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Limits = null,
        },
    };

    private sealed class FixedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
