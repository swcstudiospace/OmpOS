using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Contract tests for agentOS operations mapped to documented hosted API fields (contract agentos-sdk.dev/v1alpha1).
/// </summary>
public sealed class AgentOsContractTests
{
    private const string SecretToken = "sk_test_secret";
    private const string CreatedVmJson = """{"actor":{"actor_id":"vm-1"},"created":true}""";
    private const string NullOutputJson = """{"output":null}""";
    private static readonly string[] PermissionsOnly = ["permissions"];
    private static readonly string[] ProvisionOrder = ["/actors?namespace=ns-test", "/gateway/vm-1/action/v1.config.set", "/gateway/vm-1/action/v1.config.patch"];

    [Fact]
    public async Task ConfigSet_DenyNetworkPolicy_SendsDocumentedPermissionsShape()
    {
        using var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var provider = new AgentOsProvider(http, new Uri("https://api.rivet.dev"), "ns-test", SecretToken);

        await provider.ProvisionAsync(CreateRequest(new Dictionary<string, string>(StringComparer.Ordinal)), TestContext.Current.CancellationToken);

        var set = handler.Requests.Single(sent => string.Equals(sent.Target, "/gateway/vm-1/action/v1.config.set", StringComparison.Ordinal));
        set.Method.ShouldBe(HttpMethod.Post);
        set.RivetToken.ShouldBe(SecretToken);
        set.BearerToken.ShouldBeNull();
        var input = set.Json!["args"]!.AsArray().ShouldHaveSingleItem()!;
        input["config"]!["permissions"]!["network"]!.GetValue<string>().ShouldBe("deny");
        input["config"]!.AsObject().Select(property => property.Key).ToArray().ShouldBe(PermissionsOnly);
    }

    [Fact]
    public async Task ConfigSet_DenyNetworkPolicy_RunsBeforeEnvironmentPatch()
    {
        using var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var provider = new AgentOsProvider(http, new Uri("https://api.rivet.dev"), "ns-test", SecretToken);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["DEBUG"] = "1" };

        await provider.ProvisionAsync(CreateRequest(environment), TestContext.Current.CancellationToken);

        handler.Requests.Select(sent => sent.Target).ToArray().ShouldBe(ProvisionOrder);
        handler.Requests[2].Json!["args"]![0]!["patch"]!["environment"]!["DEBUG"]!.GetValue<string>().ShouldBe("1");
    }

    private static ProvisionRequest CreateRequest(IReadOnlyDictionary<string, string> environment) => new()
    {
        IdempotencyKey = "ompos-session-key",
        SessionId = "session-1",
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
            Environment = environment,
            Limits = null,
        },
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var rivetToken = request.Headers.TryGetValues("x-rivet-token", out var values) ? values.Single() : null;
            var target = request.RequestUri?.PathAndQuery ?? string.Empty;
            Requests.Add(new SentRequest(request.Method, target, request.Headers.Authorization?.Parameter, rivetToken, body));
            var json = target.StartsWith("/actors", StringComparison.Ordinal) ? CreatedVmJson : NullOutputJson;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private sealed record SentRequest(HttpMethod Method, string Target, string? BearerToken, string? RivetToken, string Body)
    {
        public JsonNode? Json => Body.Length == 0 ? null : JsonNode.Parse(Body);
    }
}
