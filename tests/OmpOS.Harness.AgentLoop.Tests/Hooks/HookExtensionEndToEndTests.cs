using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Tests.Plugins;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// The real omp hooks extension (<c>plugins/ompos-hooks-omp</c>, run by node) against the real bridge of a hooks service. The test runs only where
/// node is installed. It starts no omp and makes no model call: the extension's handler is called the way omp calls it, with a fake tool call.
/// </summary>
public sealed class HookExtensionEndToEndTests
{
    private static readonly TimeSpan NodeLimit = TimeSpan.FromSeconds(60);

    private static async Task<string?> NodeVersionAsync(CancellationToken ct)
    {
        try
        {
            using var node = Process.Start(new ProcessStartInfo("node", "--version") { RedirectStandardOutput = true, UseShellExecute = false });
            if (node is null)
            {
                return null;
            }

            var text = await node.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await node.WaitForExitAsync(ct).ConfigureAwait(false);
            return node.ExitCode == 0 ? text.Trim() : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static async Task<JsonElement> CallExtensionAsync(HookBridgeEndpoint endpoint, string script, string directory, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "call.mjs");
        await File.WriteAllTextAsync(path, script, ct).ConfigureAwait(false);
        var info = new ProcessStartInfo("node", ["--no-warnings", path]) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var pair in endpoint.Environment)
        {
            info.Environment[pair.Key] = pair.Value;
        }

        using var node = Process.Start(info) ?? throw new InvalidOperationException("node did not start.");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(NodeLimit);
        var output = node.StandardOutput.ReadToEndAsync(limit.Token);
        var error = node.StandardError.ReadToEndAsync(limit.Token);
        await node.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        var text = await output.ConfigureAwait(false);
        node.ExitCode.ShouldBe(0, await error.ConfigureAwait(false));
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static string Script(string bridgeFile, string command) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $$"""
            import { createHookBridge } from "{{new Uri(bridgeFile).AbsoluteUri}}";
            const bridge = createHookBridge({ env: process.env });
            const ctx = { cwd: process.cwd(), agent: { kind: "main" }, sessionManager: { getSessionId: () => "s1" } };
            const result = await bridge.handle("tool_call", { toolName: "bash", toolCallId: "c1", input: { command: "{{System.Text.Json.JsonEncodedText.Encode(command)}}" } }, ctx);
            process.stdout.write(JSON.stringify(result ?? null));
            """);

    /// <summary>
    /// The extension blocks a bash call while the guarding hook has no consent, and lets it through once the user approved the hook and the hook allowed it:
    /// the extension's request, the bridge, the dispatcher and the decision all take part.
    /// </summary>
    [Fact]
    public async Task TheRealExtensionIsBlockedUntilTheHookIsApproved()
    {
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await NodeVersionAsync(ct).ConfigureAwait(true) is not null, "node is not installed.");
        var bridgeFile = Path.Combine(HookBridgePluginTests.FindPluginsRoot(), "ompos-hooks-omp", "src", "bridge.mjs");
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: true, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        var endpoint = session.Endpoint.ShouldNotBeNull();
        var script = Script(bridgeFile, "git push origin main");

        var blocked = await CallExtensionAsync(endpoint, script, rig.StateDirectory, ct).ConfigureAwait(true);

        blocked.GetProperty("block").GetBoolean().ShouldBeTrue();
        blocked.GetProperty("reason").GetString().ShouldNotBeNull().ShouldContain("consent", Case.Insensitive);
        rig.Runner.SpawnCount.ShouldBe(0);

        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        var allowed = await CallExtensionAsync(endpoint, script, rig.StateDirectory, ct).ConfigureAwait(true);

        (allowed.ValueKind == JsonValueKind.Null || !allowed.TryGetProperty("block", out var block) || !block.GetBoolean()).ShouldBeTrue();
        rig.Runner.SpawnCount.ShouldBe(1);
        ScriptedHookRunner.InputOf(rig.Runner.Invocations[0]).ShouldContain("\"command\":\"git push origin main\"", Case.Sensitive);
    }
}
