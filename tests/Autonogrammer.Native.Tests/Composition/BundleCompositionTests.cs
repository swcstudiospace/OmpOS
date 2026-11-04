#pragma warning disable MA0002
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;
using Autonogrammer.Native.Composition.Mentions;
using Autonogrammer.Native.Composition.Plugins;
using Autonogrammer.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>Bundle servers arrive off, with plugin origin, and a plugin skill mention reads the plugin source.</summary>
public sealed class BundleCompositionTests
{
    [Fact]
    public async Task BundleServersArriveOffUntilEnabled()
    {
        var root = Path.Combine(Path.GetTempPath(), "ag-bundles-" + Guid.NewGuid().ToString("N"));
        try
        {
            var state = Path.Combine(root, "state");
            var bundle = Path.Combine(root, "plugins", "demo");
            Directory.CreateDirectory(Path.Combine(bundle, "skills"));
            await File.WriteAllTextAsync(Path.Combine(bundle, ".mcp.json"), """{"mcpServers":{"echo":{"command":"echo","args":["hi"]}}}""", TestContext.Current.CancellationToken).ConfigureAwait(true);
            var composition = new BundleComposition(Path.Combine(root, "plugins"), state, projectScope: true);
            var files = new MemoryFiles();
            var catalog = new McpServerCatalog(files, files, extra: composition.ExtraServers);
            var snapshot = await catalog.RefreshAsync(new McpLocatorContext([root], root, new Dictionary<string, string?>()), TestContext.Current.CancellationToken).ConfigureAwait(true);
            var server = snapshot.Entries.ShouldHaveSingleItem().Server;
            server.Origin.ShouldBe("plugin:demo");
            server.Transport.ShouldBe(McpTransportKind.Stdio);
            server.Priority.ShouldBeGreaterThan(10_000);
            server.EnabledInFile.ShouldBeFalse();
            snapshot.Entries[0].IsOffered.ShouldBeFalse();

            await catalog.SetEnabledAsync(server.Name, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
            catalog.Current.Entries.ShouldHaveSingleItem().IsOffered.ShouldBeFalse();
            await catalog.GrantConsentAsync(server.Name, TestContext.Current.CancellationToken).ConfigureAwait(true);
            catalog.Current.Entries.ShouldHaveSingleItem().IsOffered.ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void PluginScopeReadsThePluginSource()
    {
        var provider = new ConventionMentionProvider(["/work"], "/home");
        var method = typeof(ConventionMentionProvider).GetMethod("GetSources", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.ShouldNotBeNull();
        BundleComposition.PluginRootsFor = static _ => ["/plugins/demo"];
        try
        {
            var sources = (ConventionSources)method!.Invoke(provider, null)!;
            sources.Plugin.ShouldNotBeNull();
        }
        finally
        {
            BundleComposition.PluginRootsFor = null;
        }
    }

    private sealed class MemoryFiles : IMcpFileStore, IMcpStateStore
    {
        private McpState _state = McpState.Empty;

        public ValueTask<McpFileRead> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken) =>
            ValueTask.FromResult(McpFileRead.Missing);

        public Task WriteTextAsync(string path, string content, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<McpState> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_state);

        public Task SaveAsync(McpState state, CancellationToken cancellationToken)
        {
            _state = state;
            return Task.CompletedTask;
        }
    }

}

#pragma warning restore MA0002
