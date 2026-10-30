using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.McpGateway.Tests;

/// <summary>A real MCP client receives Cowork image bytes through the gateway.</summary>
public sealed class CoworkGatewayImageTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x11, 0x22];

    [Fact]
    public async Task RealClientReceivesImageBytesAndMimeType()
    {
        var host = new ImageCoworkToolHost(new CoworkToolResult(
            [new CoworkImageBlock(Png, "image/png", "shot", false), new CoworkTextBlock("caption", false)],
            false));
        var scope = await StartAsync(host).ConfigureAwait(true);
        await using (scope.ConfigureAwait(true))
        {
            var result = await CallAsync(scope.Gateway, TestContext.Current.CancellationToken).ConfigureAwait(true);

            result.IsError.ShouldNotBe(true);
            var image = result.Content.OfType<ImageContentBlock>().Single();
            image.MimeType.ShouldBe("image/png");
            image.DecodedData.ToArray().ShouldBe(Png);
            result.Content.OfType<TextContentBlock>().Single().Text.ShouldBe("caption");
            result.Content[0].ShouldBeOfType<ImageContentBlock>();
            result.Content[1].ShouldBeOfType<TextContentBlock>();
        }
    }

    [Fact]
    public async Task RealClientReceivesTheUntrustedEnvelopeAroundAnImage()
    {
        var host = new ImageCoworkToolHost(new CoworkToolResult(
            [new CoworkTextBlock("page text", true), new CoworkImageBlock(Png, "image/png", "page", true)],
            false));
        var scope = await StartAsync(host).ConfigureAwait(true);
        await using (scope.ConfigureAwait(true))
        {
            var result = await CallAsync(scope.Gateway, TestContext.Current.CancellationToken).ConfigureAwait(true);

            result.Content.Count.ShouldBe(3);
            result.Content[0].ShouldBeOfType<TextContentBlock>().Text.ShouldBe(McpGatewayCoworkResults.UntrustedEnvelopePrefix);
            result.Content[1].ShouldBeOfType<TextContentBlock>().Text.ShouldBe("page text");
            var image = result.Content[2].ShouldBeOfType<ImageContentBlock>();
            image.MimeType.ShouldBe("image/png");
            image.DecodedData.ToArray().ShouldBe(Png);
        }
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "GatewayScope disposes the registry.")]
    private static async Task<GatewayScope> StartAsync(ICoworkToolHost host)
    {
        var registry = new McpToolRegistry(false);
        var transferred = false;
        try
        {
            var gateway = McpGatewayServer.Start(registry, new McpToolInvoker(registry, static (_, _) => ValueTask.FromResult(true)), host);
            var scope = new GatewayScope(registry, gateway);
            transferred = true;
            return scope;
        }
        finally
        {
            if (!transferred)
            {
                await registry.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<CallToolResult> CallAsync(McpGatewayServer gateway, CancellationToken cancellationToken)
    {
        var endpoint = gateway.OpenSession("image-session");
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer " + gateway.BearerToken },
        });
        await using (transport.ConfigureAwait(false))
        {
            var client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions { ProtocolVersion = McpClientConnector.ProtocolVersion },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await using (client.ConfigureAwait(false))
            {
                return await client.CallToolAsync("browser_snap", new Dictionary<string, object?>(StringComparer.Ordinal), cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class GatewayScope : IAsyncDisposable
    {
        private readonly McpToolRegistry _registry;
        private readonly McpGatewayServer _gateway;

        public GatewayScope(McpToolRegistry registry, McpGatewayServer gateway)
        {
            _registry = registry;
            _gateway = gateway;
        }

        public McpGatewayServer Gateway => _gateway;

        public async ValueTask DisposeAsync()
        {
            await _gateway.DisposeAsync().ConfigureAwait(false);
            await _registry.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class ImageCoworkToolHost : ICoworkToolHost
    {
        private readonly CoworkToolResult _result;

        public ImageCoworkToolHost(CoworkToolResult result)
        {
            _result = result;
        }

        public event EventHandler<CoworkToolEvent>? ToolEvent
        {
            add { }
            remove { }
        }

        public ICoworkSessionLease BeginSession(CoworkSessionContext session) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<CoworkToolDescriptor>> ListOfferedAsync(string sessionId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<CoworkToolDescriptor>>([new CoworkToolDescriptor
            {
                Name = "browser_snap",
                ProviderId = "browser",
                Description = "Captures a page.",
                InputSchema = JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone(),
                Effect = CoworkToolEffect.ReadOnly,
                Approval = CoworkApprovalRequirement.ByPolicy,
                Results = CoworkResultKinds.Text | CoworkResultKinds.Image,
            }]);

        public ValueTask<CoworkToolResult> InvokeAsync(string sessionId, string toolName, JsonElement arguments, CancellationToken cancellationToken) =>
            ValueTask.FromResult(_result);
    }
}
