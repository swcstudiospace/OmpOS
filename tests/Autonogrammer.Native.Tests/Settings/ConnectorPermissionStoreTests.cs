using System.Text.Json;
using Autonogrammer.Native.Composition.Connectors;
using Autonogrammer.Presentation.Settings.Connectors;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>A connector write replaces only the MCP tokens of the user's permissions file; every other token stays.</summary>
public sealed class ConnectorPermissionStoreTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "ag-perm-store-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ConnectorWrite_KeepsUserReadDeny()
    {
        WriteUserFile("""{ "permissions": { "deny": ["Read(.env)"] } }""");
        var matrix = new ConnectorMatrixModel(new ConnectorPermissionStore(_home));

        matrix.SetToolState("remote", "echo", ConnectorToolState.Blocked);

        UserTokens("deny").ShouldContain("Read(.env)");
    }

    [Fact]
    public void ConnectorWrite_StillWritesTheMcpBlock()
    {
        WriteUserFile("""{ "permissions": { "deny": ["Read(.env)"] } }""");
        var matrix = new ConnectorMatrixModel(new ConnectorPermissionStore(_home));

        matrix.SetToolState("remote", "echo", ConnectorToolState.Blocked);

        UserTokens("deny").ShouldContain("Mcp(remote:echo)");
    }

    [Fact]
    public void ConnectorWrite_KeepsUserShellAllowToken()
    {
        WriteUserFile("""{ "permissions": { "allow": ["Shell(git)"] } }""");
        var matrix = new ConnectorMatrixModel(new ConnectorPermissionStore(_home));

        matrix.SetToolState("remote", "echo", ConnectorToolState.AlwaysAllow);

        UserTokens("allow").ShouldContain("Shell(git)");
        UserTokens("allow").ShouldContain("Mcp(remote:echo)");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    private void WriteUserFile(string json)
    {
        Directory.CreateDirectory(Path.Combine(_home, ".autonogrammer"));
        File.WriteAllText(Path.Combine(_home, ".autonogrammer", "permissions.json"), json);
    }

    private IReadOnlyList<string> UserTokens(string list)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_home, ".autonogrammer", "permissions.json")));
        return [.. document.RootElement.GetProperty("permissions").GetProperty(list).EnumerateArray().Select(item => item.GetString() ?? string.Empty)];
    }
}
