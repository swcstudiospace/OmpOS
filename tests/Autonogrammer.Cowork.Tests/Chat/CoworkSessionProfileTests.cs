using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.AgentLoop.Launch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Chat;

/// <summary>The Cowork launch differs from Code in every security-relevant flag.</summary>
public sealed class CoworkSessionProfileTests
{
    [Theory]
    [InlineData(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Manual, "always-ask-cached")]
    // D-12: Auto maps to always-ask-cached, the same cached ask as Manual. Skip stays always-ask.
    [InlineData(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Auto, "always-ask-cached")]
    [InlineData(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip, "always-ask")]
    public void EachModeLaunchesWithoutExtensionsAndNeverYolo(Autonogrammer.Cowork.Tools.CoworkPermissionMode mode, string approval)
    {
        using var fixture = new TempTree();
        var folder = fixture.Directory("work");
        var other = fixture.Directory("other");
        var session = fixture.Directory("session");
        var profile = new CoworkSessionProfile(session, folder, [folder, other], mode, gatewayUrl: new Uri("http://127.0.0.1:9/mcp"), gatewayBearer: "secret-bearer");

        var plan = profile.Create();

        var overlay = SessionStateLayout.ConfigOverlayFile(Path.GetFullPath(session));
        var prompt = Path.Combine(Path.GetFullPath(session), CoworkSystemPrompt.FileName);
        plan.ExtraArguments.ShouldBe(new[]
        {
            "--no-extensions",
            "--approval-mode",
            approval,
            "--add-dir",
            Path.GetFullPath(folder),
            "--add-dir",
            Path.GetFullPath(other),
            "--append-system-prompt",
            prompt,
            "--config",
            overlay,
        });
        string.Join(' ', plan.ExtraArguments).ShouldNotContain("yolo");
        plan.ConfigOverlayPath.ShouldBe(overlay);
        CoworkSessionProfile.OverlayYaml.ShouldContain("browser:");
        CoworkSessionProfile.OverlayYaml.ShouldContain("enabled: false");
        CoworkSessionProfile.OverlayYaml.ShouldContain("computer:");
        plan.McpServers.Count.ShouldBe(1);
        plan.McpServers[0].Name.ShouldBe("cowork");
        plan.McpServers[0].Headers["Authorization"].ShouldBe("Bearer secret-bearer");
        plan.ToString().ShouldNotContain("secret-bearer");
        plan.HooksBridgeLoaded.ShouldBeFalse();
        plan.LoadedPluginIds.ShouldBeEmpty();
        File.Exists(prompt).ShouldBeTrue();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(prompt).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void NoGatewayMeansNoCatalogServer()
    {
        using var fixture = new TempTree();
        var folder = fixture.Directory("work");
        var plan = new CoworkSessionProfile(fixture.Directory("session"), folder, [folder]).Create();

        plan.McpServers.ShouldBeEmpty();
        plan.Environment.ShouldBeEmpty();
    }

    [Fact]
    public void TheEnvironmentKeepsProviderKeysAndRefusesEverythingElse()
    {
        using var fixture = new TempTree();
        var folder = fixture.Directory("work");
        var keys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AUTONOGRAMMER_PROVIDER_MINE_KEY"] = "provider-secret",
        };
        var plan = new CoworkSessionProfile(fixture.Directory("session"), folder, [folder], providerKeys: keys).Create();

        plan.Environment.Count.ShouldBe(1);
        plan.Environment["AUTONOGRAMMER_PROVIDER_MINE_KEY"].ShouldBe("provider-secret");
        plan.Environment.Keys.ShouldAllBe(static key => CoworkSessionProfile.IsProviderKeyName(key));

        var poisoned = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AUTONOGRAMMER_PROVIDER_MINE_KEY"] = "provider-secret",
            ["SUBSTRATE_URL"] = "http://substrate.invalid",
            ["AUTONOGRAMMER_HOOKS_TOKEN"] = "hook-secret",
            ["AUTONOGRAMMER_API_TOKEN"] = "api-secret",
            ["AUTONOGRAMMER_ACCOUNT_TOKEN"] = "account-secret",
        };
        var refused = Should.Throw<CoworkEnvironmentRefusedException>(() => CoworkSessionProfile.FilterProviderKeys(poisoned));
        refused.Message.ShouldNotContain("hook-secret");
        refused.Message.ShouldNotContain("api-secret");
        refused.Message.ShouldNotContain("account-secret");
        refused.Message.ShouldNotContain("http://substrate.invalid");
    }

    [Fact]
    public void AnUnwritablePromptRefusesTheStart()
    {
        using var fixture = new TempTree();
        var folder = fixture.Directory("work");
        var blocking = fixture.File("not-a-directory");
        var profile = new CoworkSessionProfile(blocking, folder, [folder]);

        var refused = Should.Throw<CoworkPromptUnavailableException>(() => profile.Create());
        refused.Message.Length.ShouldBeGreaterThan(0);
        refused.Message.ShouldNotContain("Code");
    }

    private sealed class TempTree : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "cowork-profile-" + Guid.NewGuid().ToString("N"));

        public void Dispose() => System.IO.Directory.Delete(_root, recursive: true);

        public string Directory(string name)
        {
            var path = Path.Combine(_root, name);
            System.IO.Directory.CreateDirectory(path);
            return path;
        }

        public string File(string name)
        {
            var path = Path.Combine(_root, name);
            System.IO.File.WriteAllText(path, "x");
            return path;
        }

    }
}
