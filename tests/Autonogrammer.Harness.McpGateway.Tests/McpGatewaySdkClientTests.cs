using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Autonogrammer.Harness.Mcp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.McpGateway.Tests;

/// <summary>The gateway interoperates with the MCP SDK's Streamable HTTP client, the client an omp-like host would use.</summary>
public sealed class McpGatewaySdkClientTests
{
    [Fact]
    public async Task SdkClientListsOnlyGrantedToolsAndCallsThemThroughTheGateway()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: false, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            harness.Registry.Enable("sdk-session", GatewayHarness.Add);
            var endpoint = harness.Gateway.OpenSession("sdk-session");
            var transport = CreateTransport(endpoint, harness.Bearer);
            await using var transportScope = transport.ConfigureAwait(true);
            var client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions { ProtocolVersion = McpClientConnector.ProtocolVersion },
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
            await using (client.ConfigureAwait(true))
            {
                var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
                tools.Select(static tool => tool.Name).ShouldBe(["fixture__add"]);

                var result = await client.CallToolAsync(
                    "fixture__add",
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["a"] = 4, ["b"] = 5 },
                    cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

                result.IsError.ShouldNotBe(true);
                result.Content.OfType<TextContentBlock>().Single().Text.ShouldBe("9");
                harness.Log.ReceivedTools.ShouldBe(["add"]);
                harness.Approval.Requests.Single().Tool.ShouldBe(GatewayHarness.Add);
            }
        }
    }

    [Fact]
    public async Task SdkClientWithoutTheSecretCannotConnect()
    {
        var harness = await GatewayHarness.StartAsync(enabledByDefault: true, approve: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (harness.ConfigureAwait(true))
        {
            var endpoint = harness.Gateway.OpenSession("sdk-session");

            await Should.ThrowAsync<Exception>(() => McpClient.CreateAsync(
                CreateTransport(endpoint, "Bearer wrong-value"),
                new McpClientOptions { ProtocolVersion = McpClientConnector.ProtocolVersion },
                cancellationToken: TestContext.Current.CancellationToken)).ConfigureAwait(true);

            harness.Log.ReceivedTools.ShouldBeEmpty();
        }
    }

    private static HttpClientTransport CreateTransport(Uri endpoint, string authorization) =>
        new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = authorization },
        });
}
