using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests;

/// <summary>
/// Verifies the load pipeline through consumer-visible outcomes: what is loaded, rejected and quarantined, which
/// plugins were activated, and whether any process was started.
/// </summary>
public sealed class PluginHostLoadTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HostileManifestWithEntryIsRejectedAndItsSentinelIsNeverWritten()
    {
        using var directory = new TempDirectory();
        var sentinel = directory.PathOf("sentinel.txt");
        var hostile = ManifestJson.Manifest("evil.plugin");
        hostile["entry"] = new JsonObject
        {
            ["command"] = ManifestJson.Strings("/bin/sh", "-c", $"touch '{sentinel}'"),
        };
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var activator = new RecordingActivator();
        using var host = new PluginHost(store, activator, PluginApiVersionRange.Current);
        var runner = new FakeHookProcessRunner();

        var report = await host.LoadAsync([ManifestJson.Source(hostile)], Token);
        var run = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        report.Loaded.ShouldBeEmpty();
        report.Rejected.Single().Kind.ShouldBe(PluginRejectionKind.UnknownField);
        report.Rejected.Single().Message.ShouldContain("entry");
        activator.Activated.ShouldBeEmpty();
        host.Hooks.List().ShouldBeEmpty();
        run.Executions.ShouldBeEmpty();
        runner.SpawnCount.ShouldBe(0);
        File.Exists(sentinel).ShouldBeFalse();
    }

    [Fact]
    public async Task HostileHookFieldIsRejectedBeforeAnyHookIsRegistered()
    {
        using var directory = new TempDirectory();
        var sentinel = directory.PathOf("sentinel.txt");
        var hook = ManifestJson.Hook("sessionStart", ["/bin/sh", "-c", $"touch '{sentinel}'"], directory.Path);
        hook["run"] = ManifestJson.Value(true);
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = new PluginHost(store, new RecordingActivator(), PluginApiVersionRange.Current);

        var report = await host.LoadAsync([ManifestJson.Source(ManifestJson.Manifest("evil.plugin", hooks: hook))], Token);

        report.Rejected.Single().Kind.ShouldBe(PluginRejectionKind.UnknownField);
        host.Hooks.List().ShouldBeEmpty();
        File.Exists(sentinel).ShouldBeFalse();
    }

    [Fact]
    public async Task IncompatibleApiVersionIsRejectedNamingThePluginAndTheSupportedRange()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var activator = new RecordingActivator();
        using var host = new PluginHost(store, activator, new PluginApiVersionRange(1, 1));

        var report = await host.LoadAsync([ManifestJson.Source(ManifestJson.Manifest("acme.tools", apiVersion: 2))], Token);

        var rejection = report.Rejected.Single();
        rejection.Kind.ShouldBe(PluginRejectionKind.UnsupportedApiVersion);
        rejection.PluginId.ShouldBe("acme.tools");
        rejection.Message.ShouldContain("acme.tools");
        rejection.Message.ShouldContain("requires API version 2");
        rejection.Message.ShouldContain("1 to 1");
        activator.Activated.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApiVersionRangeIncludesBothEndsAndRejectsOneBeyond()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = new PluginHost(store, new RecordingActivator(), new PluginApiVersionRange(1, 2));

        var report = await host.LoadAsync(
        [
            ManifestJson.Source(ManifestJson.Manifest("at.minimum", apiVersion: 1)),
            ManifestJson.Source(ManifestJson.Manifest("at.maximum", apiVersion: 2)),
            ManifestJson.Source(ManifestJson.Manifest("beyond.maximum", apiVersion: 3)),
        ], Token);

        report.Loaded.Select(plugin => plugin.PluginId).ShouldBe(["at.minimum", "at.maximum"]);
        report.Rejected.Single().PluginId.ShouldBe("beyond.maximum");
        report.Rejected.Single().Kind.ShouldBe(PluginRejectionKind.UnsupportedApiVersion);
    }

    [Fact]
    public async Task PluginWhoseActivationThrowsAsyncIsQuarantinedAndOthersKeepLoading()
    {
        using var directory = new TempDirectory();
        var bad = ManifestJson.Manifest("bad.plugin");
        var good = ManifestJson.Manifest("good.plugin");
        var activator = new RecordingActivator(async (manifest, _) =>
        {
            await Task.Yield();
            if (string.Equals(manifest.Id, "bad.plugin", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("boom during activation");
            }
        });
        string reason;
        using (var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token))
        using (var host = new PluginHost(store, activator, PluginApiVersionRange.Current))
        {
            var report = await host.LoadAsync([ManifestJson.Source(bad), ManifestJson.Source(good)], Token);

            report.Loaded.Select(plugin => plugin.PluginId).ShouldBe(["good.plugin"]);
            var quarantined = report.Quarantined.Single();
            quarantined.PluginId.ShouldBe("bad.plugin");
            quarantined.Reason.ShouldContain("boom during activation");
            reason = quarantined.Reason;
            host.Hooks.List().ShouldBeEmpty();
        }

        // The quarantine is persisted: after a restart the plugin is not activated again, and the reason is unchanged.
        using var reopened = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var secondActivator = new RecordingActivator();
        using var secondHost = new PluginHost(reopened, secondActivator, PluginApiVersionRange.Current);

        var secondReport = await secondHost.LoadAsync([ManifestJson.Source(bad), ManifestJson.Source(good)], Token);

        secondActivator.Activated.ShouldBe(["good.plugin"]);
        secondReport.Quarantined.Single().Reason.ShouldBe(reason);
    }

    [Fact]
    public async Task SecondManifestWithAnAlreadyLoadedIdentifierIsRejected()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var activator = new RecordingActivator();
        using var host = new PluginHost(store, activator, PluginApiVersionRange.Current);

        var report = await host.LoadAsync(
            [ManifestJson.Source(ManifestJson.Manifest("acme.tools")), ManifestJson.Source(ManifestJson.Manifest("acme.tools"))],
            Token);

        report.Loaded.Count.ShouldBe(1);
        report.Rejected.Single().Kind.ShouldBe(PluginRejectionKind.DuplicatePluginId);
        activator.Activated.ShouldBe(["acme.tools"]);
    }
}
