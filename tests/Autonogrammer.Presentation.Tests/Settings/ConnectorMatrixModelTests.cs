using Autonogrammer.Presentation.Settings.Connectors;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The three-state matrix writes Mcp tokens and reads them back, including server-level overrides.</summary>
public sealed class ConnectorMatrixModelTests
{
    [Fact]
    public void ToolStates_RoundTripThroughTheStore()
    {
        var store = new MemoryStore();
        var matrix = new ConnectorMatrixModel(store);

        matrix.SetToolState("alpha", "echo", ConnectorToolState.AlwaysAllow);
        matrix.SetToolState("alpha", "rm", ConnectorToolState.Blocked);
        matrix.SetToolState("alpha", "ask", ConnectorToolState.NeedsApproval);

        var reloaded = new ConnectorMatrixModel(store);
        reloaded.ToolState("alpha", "echo").ShouldBe(ConnectorToolState.AlwaysAllow);
        reloaded.ToolState("alpha", "rm").ShouldBe(ConnectorToolState.Blocked);
        reloaded.ToolState("alpha", "ask").ShouldBe(ConnectorToolState.NeedsApproval);
        store.Allow.ShouldBe(["Mcp(alpha:echo)"]);
        store.Deny.ShouldBe(["Mcp(alpha:rm)"]);
    }

    [Fact]
    public void ServerBlocked_DeniesEveryTool_AndServerAllow_YieldsToAToolDeny()
    {
        var store = new MemoryStore();
        var matrix = new ConnectorMatrixModel(store);
        matrix.SetToolState("alpha", "echo", ConnectorToolState.AlwaysAllow);
        matrix.SetServerState("alpha", ConnectorToolState.Blocked);

        matrix.ToolState("alpha", "echo").ShouldBe(ConnectorToolState.Blocked);
        matrix.ToolState("alpha", "other").ShouldBe(ConnectorToolState.Blocked);
        store.Allow.ShouldContain("Mcp(alpha:echo)", StringComparer.Ordinal);

        matrix.SetServerState("alpha", ConnectorToolState.AlwaysAllow);
        matrix.SetToolState("alpha", "rm", ConnectorToolState.Blocked);
        matrix.ToolState("alpha", "echo").ShouldBe(ConnectorToolState.AlwaysAllow);
        matrix.ToolState("alpha", "rm").ShouldBe(ConnectorToolState.Blocked);
        matrix.ToolState("alpha", "fresh").ShouldBe(ConnectorToolState.AlwaysAllow);
    }

    [Fact]
    public void PageCopy_StatesThatAutoDoesNotTrustReadOnlyHint()
    {
        ConnectorMatrixModel.ReadOnlyHintNotice.ShouldContain("Auto does not trust readOnlyHint");
        ConnectorMatrixModel.ReadOnlyHintNotice.ShouldContain("readOnlyHint");
    }

    private sealed class MemoryStore : IConnectorPermissionStore
    {
        public List<string> Allow { get; private set; } = [];

        public List<string> Deny { get; private set; } = [];

        public IReadOnlyList<string> ReadAllowTokens() => Allow;

        public IReadOnlyList<string> ReadDenyTokens() => Deny;

        public void ReplaceAllowTokens(IReadOnlyList<string> tokens) => Allow = [.. tokens];

        public void ReplaceDenyTokens(IReadOnlyList<string> tokens) => Deny = [.. tokens];
    }
}
