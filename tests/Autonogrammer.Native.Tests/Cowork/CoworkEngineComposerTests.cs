#pragma warning disable CA2007
#pragma warning disable MA0002

using Autonogrammer.Cowork.Chat;
using Autonogrammer.Cowork.Instructions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Settings.Models;
using Autonogrammer.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>The composer hands the profile provider keys only, composes with an allow-list, and writes the session's context into the launch files.</summary>
public sealed class CoworkEngineComposerTests
{
    [Fact]
    public async Task AllowlistKeepsProviderKeysAndDropsOtherNames()
    {
        using var root = new TempDir();
        var omp = new ScriptedOmp { AdvertiseAuthentication = true };
        var store = new InMemorySessionSecretStore();
        await store.SetAsync("AUTONOGRAMMER_PROVIDER_OPENAI_KEY", new SecretString("secret-value"), TestContext.Current.CancellationToken);
        var keys = new ProviderKeyEnvironment(store);
        await keys.RefreshAsync(TestContext.Current.CancellationToken);
        var composed = CoworkEngineComposer.Compose(Request(root.Path, omp, keys));
        await using var host = composed.Engine.Host;
        await host.ConnectAsync(TestContext.Current.CancellationToken);
        var launch = omp.Launches.ShouldHaveSingleItem();
        launch.EnvironmentMode.ShouldBe(EngineEnvironmentMode.Allowlist);
        if (!OperatingSystem.IsWindows())
        {
            launch.EnvironmentAllowList.ShouldBeEmpty();
        }

        launch.EnvironmentVariables.Keys.ShouldBe(["AUTONOGRAMMER_PROVIDER_OPENAI_KEY"]);
        var policy = new EngineEnvironmentPolicy(launch.EnvironmentMode, launch.EnvironmentAllowList);
        policy.IsAllowed("PATH").ShouldBeTrue();
        policy.IsAllowed("HOME").ShouldBeTrue();
        policy.IsAllowed("AUTONOGRAMMER_API_TOKEN").ShouldBeFalse();
        policy.IsAllowed("SUPER_SECRET").ShouldBeFalse();
        CoworkEngineComposer.WindowsAllowListExtras.ShouldContain("USERPROFILE");
    }

    [Fact]
    public void TheWrittenPromptAndOverlayCarryTheSessionContextAndMemoryOff()
    {
        using var root = new TempDir();
        var standing = StandingContextBuilder.Build("global-canary", "project-canary", null, "brief-canary");
        var composed = CoworkEngineComposer.Compose(Request(root.Path, new ScriptedOmp(), null) with { Standing = standing });

        var prompt = File.ReadAllText(Path.Combine(root.Path, CoworkSystemPrompt.FileName));
        prompt.ShouldContain(StandingContextBuilder.SystemPromptParagraph);
        prompt.ShouldNotContain("global-canary");
        var overlayPath = composed.Plan.ConfigOverlayPath!;
        var overlay = File.ReadAllText(overlayPath);
        overlay.ShouldContain("browser:\n  enabled: false");
        overlay.ShouldContain("memory:\n  backend: off");
        composed.Plan.ExtraArguments.Count(argument => string.Equals(argument, "--config", StringComparison.Ordinal)).ShouldBe(1);
        composed.Plan.ExtraArguments.ShouldContain(overlayPath);
        var block = composed.Plan.FirstPromptBlocks.ShouldHaveSingleItem();
        block.ShouldContain("global-canary");
        block.ShouldContain("project-canary");
        block.ShouldContain("brief-canary");
    }

    [Fact]
    public void ASessionWithoutAccountDataHasNoFirstPromptBlockButStillTurnsOmpMemoryOff()
    {
        using var root = new TempDir();
        var composed = CoworkEngineComposer.Compose(Request(root.Path, new ScriptedOmp(), null));

        composed.Plan.FirstPromptBlocks.ShouldBeEmpty();
        var overlay = File.ReadAllText(composed.Plan.ConfigOverlayPath!);
        overlay.ShouldContain("memory:\n  backend: off");
    }

    [Fact]
    public void AnAbsentProviderKeySourceIsAnEmptyDictionary()
    {
        using var root = new TempDir();
        var composed = CoworkEngineComposer.Compose(Request(root.Path, new ScriptedOmp(), null));
        composed.Plan.Environment.ShouldBeEmpty();
    }

    [Fact]
    public void ACatalogServerNamedCoworkRaisesANotice()
    {
        using var root = new TempDir();
        var offered = new AcpHttpMcpServer("cowork", new Uri("http://127.0.0.1:9/mcp"), new Dictionary<string, string>(StringComparer.Ordinal));
        var composed = CoworkEngineComposer.Compose(Request(root.Path, new ScriptedOmp(), null) with { OfferedServers = [offered] });
        composed.Notices.ShouldContain(CoworkEngineComposer.CoworkNameClashNotice);
    }

    private static CoworkEngineRequest Request(string root, ScriptedOmp omp, ProviderKeyEnvironment? keys) =>
        new(root, root, [root], new CoworkPathBoundary(root), static () => false)
        {
            SessionId = "task-under-test",
            TransportFactory = omp.TransportFactory,
            ProviderKeys = keys,
        };

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cowork-compose-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, true);
    }
}

#pragma warning restore MA0002
#pragma warning restore CA2007
