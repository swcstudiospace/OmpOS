using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Mcp.Tests;

/// <summary>Every model tool call passes the enablement check and then the injected approval before the server sees it.</summary>
public sealed class McpToolInvokerTests : IDisposable
{
    private static readonly McpToolId Add = new(FixtureRegistry.ServerName, "add");
    private static readonly McpToolId Echo = new(FixtureRegistry.ServerName, "echo");

    private readonly CallLogFile _log = new();

    /// <inheritdoc />
    public void Dispose() => _log.Dispose();

    [Fact]
    public async Task UnapprovedCall_IsDenied_AndTheServerIsNotContacted()
    {
        var registry = await FixtureRegistry.CreateAsync(_log.Path, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            var approval = new CountingApproval(decision: false);
            var invoker = new McpToolInvoker(registry, approval.Approve);

            await Should.ThrowAsync<McpToolDeniedException>(() =>
                invoker.InvokeAsync("session-a", Add, Args(2, 3), TestContext.Current.CancellationToken).AsTask()).ConfigureAwait(true);

            approval.Calls.ShouldBe(1);
            _log.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ApprovedCall_ReachesTheServer_AndReturnsItsResult()
    {
        var registry = await FixtureRegistry.CreateAsync(_log.Path, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            var approval = new CountingApproval(decision: true);
            var invoker = new McpToolInvoker(registry, approval.Approve);

            CallToolResult result = await invoker.InvokeAsync("session-a", Add, Args(2, 3), TestContext.Current.CancellationToken).ConfigureAwait(true);

            result.IsError.ShouldNotBe(true);
            result.Content.OfType<TextContentBlock>().Single().Text.ShouldBe("5");
            _log.ReceivedTools.ShouldBe(["add"]);

            approval.Calls.ShouldBe(1);
            var request = approval.Requests.Single();
            request.SessionId.ShouldBe("session-a");
            request.Tool.ShouldBe(Add);
            request.Arguments["a"].ShouldBe(2);
            request.Arguments["b"].ShouldBe(3);
        }
    }

    [Fact]
    public async Task DisabledTool_IsRefusedBeforeApproval_AndTheServerIsNotContacted()
    {
        var registry = await FixtureRegistry.CreateAsync(_log.Path, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            registry.Disable("session-a", Echo);
            var approval = new CountingApproval(decision: true);
            var invoker = new McpToolInvoker(registry, approval.Approve);

            await Should.ThrowAsync<McpToolNotOfferedException>(() =>
                invoker.InvokeAsync("session-a", Echo, Text("hi"), TestContext.Current.CancellationToken).AsTask()).ConfigureAwait(true);

            approval.Calls.ShouldBe(0);
            _log.ReceivedTools.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ToolDisabledForOneSession_IsCallableFromAnotherSession_WhenApproved()
    {
        var registry = await FixtureRegistry.CreateAsync(_log.Path, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using (registry.ConfigureAwait(true))
        {
            registry.Disable("session-a", Echo);
            var approval = new CountingApproval(decision: true);
            var invoker = new McpToolInvoker(registry, approval.Approve);

            CallToolResult result = await invoker.InvokeAsync("session-b", Echo, Text("hi"), TestContext.Current.CancellationToken).ConfigureAwait(true);

            result.Content.OfType<TextContentBlock>().Single().Text.ShouldBe("hi");
            _log.ReceivedTools.ShouldBe(["echo"]);
        }
    }

    private static Dictionary<string, object?> Args(int a, int b) =>
        new(StringComparer.Ordinal) { ["a"] = a, ["b"] = b };

    private static Dictionary<string, object?> Text(string text) =>
        new(StringComparer.Ordinal) { ["text"] = text };
}
