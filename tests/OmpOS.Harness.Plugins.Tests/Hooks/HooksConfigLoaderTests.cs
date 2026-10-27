using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies the effective hook set: merge order across sources, deduplication, defaults, diagnostics for bad files, managed
/// pre-consent, plugin hooks and the queries the bridge relies on.
/// </summary>
public sealed class HooksConfigLoaderTests
{
    private static readonly string Home = Path.Combine("/home", "ada");
    private static readonly string Root = Path.Combine("/work", "app");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Config(string events) => $$"""{ "version": 1, "hooks": { {{events}} } }""";

    private static Task<EffectiveHookSet> LoadAsync(MemoryHookFileSource files, IReadOnlyList<HookRegistration>? plugins = null) =>
        new HooksConfigLoader(files).LoadAsync(new HookLocatorContext([Root], Home, HookPlatform.Linux), plugins, Token);

    [Fact]
    public async Task HooksFromEverySourceLoadInPriorityThenFileOrder()
    {
        var files = new MemoryHookFileSource()
            .With(HookSourceLocator.ManagedPath(HookPlatform.Linux, string.Empty), Config("""
                "beforeShellExecution": [{ "command": "managed.sh" }]
                """), tamperResistant: true)
            .With(Path.Combine(Root, ".cursor", "hooks.json"), Config("""
                "beforeShellExecution": [{ "command": "project-a.sh" }, { "command": "project-b.sh" }]
                """))
            .With(Path.Combine(Home, ".cursor", "hooks.json"), Config("""
                "beforeShellExecution": [{ "command": "user.sh" }]
                """))
            .With(Path.Combine(Root, ".claude", "settings.json"), """{ "hooks": { "PreToolUse": [{ "hooks": [{ "type": "command", "command": "claude.sh" }] }] } }""");

        var set = await LoadAsync(files);

        set.For(HookEvent.BeforeShellExecution).Select(hook => hook.Command).ShouldBe(
            ["managed.sh", "project-a.sh", "project-b.sh", "user.sh"],
            ignoreOrder: false);
        set.For(HookEvent.PreToolUse).Single().Command.ShouldBe("claude.sh", Case.Sensitive);
        set.Sources.Count.ShouldBe(4);
    }

    [Fact]
    public async Task DefaultsFollowTheSourceFormat()
    {
        var files = new MemoryHookFileSource()
            .With(Path.Combine(Root, ".cursor", "hooks.json"), Config("""
                "stop": [{ "command": "./stop.sh" }],
                "afterFileEdit": [{ "args": ["prettier", "--write"] }]
                """))
            .With(Path.Combine(Root, ".claude", "settings.json"), """{ "hooks": { "Stop": [{ "hooks": [{ "type": "command", "command": "./claude-stop.sh" }] }] } }""");

        var set = await LoadAsync(files);

        var cursorStop = set.For(HookEvent.Stop).Single(hook => hook.Command == "./stop.sh");
        cursorStop.LoopLimit.ShouldBe(5);
        cursorStop.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        cursorStop.Shell.ShouldBe(HookShell.PosixSh);
        cursorStop.WorkingDirectory.ShouldBe(Root, Case.Sensitive);
        cursorStop.HashVersion.ShouldBe(2);
        set.For(HookEvent.Stop).Single(hook => hook.Command == "./claude-stop.sh").LoopLimit.ShouldBeNull();
        var args = set.For(HookEvent.AfterFileEdit).Single();
        args.Shell.ShouldBe(HookShell.None);
        args.Args!.ShouldBe(["prettier", "--write"], ignoreOrder: false);
    }

    [Fact]
    public async Task HooksOfOneProjectInOmpOsAndCursorFilesRunOnce()
    {
        var same = Config("""
            "preToolUse": [{ "command": ".cursor/hooks/check.sh", "matcher": "Shell" }]
            """);
        var files = new MemoryHookFileSource()
            .With(Path.Combine(Root, ".ompos", "hooks.json"), same)
            .With(Path.Combine(Root, ".cursor", "hooks.json"), same);

        var set = await LoadAsync(files);

        set.For(HookEvent.PreToolUse).Count.ShouldBe(1);
        set.Diagnostics.ShouldContain(d => d.Severity == HookDiagnosticSeverity.Warning && d.Message.Contains("runs once", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADifferentMatcherIsNotADuplicate()
    {
        var files = new MemoryHookFileSource()
            .With(Path.Combine(Root, ".ompos", "hooks.json"), Config("""
                "preToolUse": [{ "command": "./check.sh", "matcher": "Shell" }]
                """))
            .With(Path.Combine(Root, ".cursor", "hooks.json"), Config("""
                "preToolUse": [{ "command": "./check.sh", "matcher": "Read" }]
                """));

        var set = await LoadAsync(files);

        set.For(HookEvent.PreToolUse).Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnInvalidFileBecomesDiagnosticsAndDoesNotStopTheOthers()
    {
        var files = new MemoryHookFileSource()
            .With(Path.Combine(Root, ".ompos", "hooks.json"), "{ broken")
            .With(Path.Combine(Root, ".cursor", "hooks.json"), Config("""
                "stop": [{ "command": "./ok.sh" }]
                """));

        var set = await LoadAsync(files);

        set.For(HookEvent.Stop).Single().Command.ShouldBe("./ok.sh", Case.Sensitive);
        set.Diagnostics.ShouldContain(d => d.Severity == HookDiagnosticSeverity.Error && d.File.EndsWith(".ompos/hooks.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFileOverTheSizeLimitIsReportedNotThrown()
    {
        var big = Config($$"""
            "stop": [{ "command": "./ok.sh", "description": "{{new string('x', HookConfigLimits.MaxHooksFileBytes)}}" }]
            """);
        var files = new MemoryHookFileSource().With(Path.Combine(Root, ".cursor", "hooks.json"), big);

        var set = await LoadAsync(files);

        set.Hooks.ShouldBeEmpty();
        set.Diagnostics.Single().Severity.ShouldBe(HookDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task HookIdsAreUniqueAndCarryThePseudoPluginId()
    {
        var files = new MemoryHookFileSource()
            .With(Path.Combine(Root, ".ompos", "hooks.json"), Config("""
                "stop": [{ "command": "./a.sh", "id": "audit" }]
                """))
            .With(Path.Combine(Root, ".cursor", "hooks.json"), Config("""
                "stop": [{ "command": "./b.sh", "id": "audit" }, { "command": "./c.sh" }]
                """));

        var set = await LoadAsync(files);

        var ids = set.Hooks.Select(hook => hook.Id).ToList();
        ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(ids.Count);
        set.Hooks[0].Id.ShouldBe($"ompos-project:{HookSourceLocator.RootHash(Root)}:audit", Case.Sensitive);
        set.Hooks[2].Id.ShouldBe($"cursor-project:{HookSourceLocator.RootHash(Root)}:stop:1", Case.Sensitive);
        set.Hooks.Select(hook => hook.PluginId).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
    }

    [Fact]
    public async Task ManagedHooksAreOnlyPreConsentedWhenTheirFileIsTamperResistant()
    {
        var content = Config("""
            "beforeShellExecution": [{ "command": "managed.sh" }]
            """);
        var managed = HookSourceLocator.ManagedPath(HookPlatform.Linux, string.Empty);

        var resistant = await LoadAsync(new MemoryHookFileSource().With(managed, content, tamperResistant: true));
        var writable = await LoadAsync(new MemoryHookFileSource().With(managed, content));

        resistant.Hooks.Single().PreConsented.ShouldBeTrue();
        writable.Hooks.Single().PreConsented.ShouldBeFalse();
        writable.Diagnostics.ShouldContain(d => d.Severity == HookDiagnosticSeverity.Warning && d.Message.Contains("pre-consented", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProjectHooksAreNeverPreConsented()
    {
        var files = new MemoryHookFileSource().With(
            Path.Combine(Root, ".cursor", "hooks.json"),
            Config("""
                "stop": [{ "command": "./a.sh" }]
                """),
            tamperResistant: true);

        var set = await LoadAsync(files);

        set.Hooks.Single().PreConsented.ShouldBeFalse();
    }

    [Fact]
    public async Task PluginManifestHooksFollowEveryFileSourceAndKeepTheirVersionOneHash()
    {
        var command = new HookCommand(["/opt/plugin/check", "--strict"], "/opt/plugin");
        var hash = HookCommandIdentity.ComputeHash(HookTrigger.BeforeAction, true, command);
        var registration = new HookRegistration("acme#0", "acme", HookTrigger.BeforeAction, command, hash, true, true);
        var files = new MemoryHookFileSource().With(
            Path.Combine(Root, ".cursor", "hooks.json"),
            Config("""
                "preToolUse": [{ "command": "./a.sh" }]
                """));

        var set = await LoadAsync(files, [registration]);

        var hooks = set.For(HookEvent.PreToolUse);
        hooks.Count.ShouldBe(2);
        var plugin = hooks[1];
        plugin.Scope.ShouldBe(HookScope.Plugin);
        plugin.CommandHash.ShouldBe(hash, Case.Sensitive);
        plugin.HashVersion.ShouldBe(1);
        plugin.Shell.ShouldBe(HookShell.None);
        plugin.Args!.ShouldBe(["/opt/plugin/check", "--strict"], ignoreOrder: false);
        plugin.Blocking.ShouldBeTrue();
        plugin.Id.ShouldBe("acme#0", Case.Sensitive);
    }

    [Theory]
    [InlineData(HookTrigger.SessionStart, HookEvent.SessionStart)]
    [InlineData(HookTrigger.BeforeAction, HookEvent.PreToolUse)]
    [InlineData(HookTrigger.AfterAction, HookEvent.PostToolUse)]
    [InlineData(HookTrigger.SessionEnd, HookEvent.SessionEnd)]
    public async Task PluginTriggersMapToCursorEvents(HookTrigger trigger, HookEvent expected)
    {
        var command = new HookCommand(["/opt/p/x"], "/opt/p");
        var registration = new HookRegistration("p#0", "p", trigger, command, HookCommandIdentity.ComputeHash(trigger, false, command), false, true);

        var set = await LoadAsync(new MemoryHookFileSource(), [registration]);

        set.For(expected).Single().Id.ShouldBe("p#0", Case.Sensitive);
    }

    [Fact]
    public async Task OmpSectionAndUnknownEventHooksAreIndexedApartFromCursorEvents()
    {
        var files = new MemoryHookFileSource().With(
            Path.Combine(Root, ".cursor", "hooks.json"),
            """{ "version": 1, "hooks": { "futureEvent": [{ "command": "./f.sh" }] }, "omp": { "tool_call": [{ "command": "./g.sh" }] } }""");

        var set = await LoadAsync(files);

        set.Hooks.Count.ShouldBe(2);
        set.ForOmp("tool_call").Single().Command.ShouldBe("./g.sh", Case.Sensitive);
        set.ForOmp("tool_call").Single().Blocking.ShouldBeTrue();
        set.ForOmp("input").ShouldBeEmpty();
        set.ActiveOmpEvents().ShouldBe(["tool_call"], ignoreOrder: true);
        set.ActiveEvents().ShouldBeEmpty();
    }

    [Fact]
    public async Task ActiveAndFailClosedEventSetsDescribeWhatTheBridgeMustGuard()
    {
        var managed = HookSourceLocator.ManagedPath(HookPlatform.Linux, string.Empty);
        var files = new MemoryHookFileSource()
            .With(managed, Config("""
                "beforeReadFile": [{ "command": "managed-read.sh" }],
                "afterFileEdit": [{ "command": "managed-fmt.sh" }]
                """), tamperResistant: true)
            .With(Path.Combine(Root, ".cursor", "hooks.json"), Config("""
                "beforeShellExecution": [{ "command": "./a.sh", "failClosed": true }],
                "preToolUse": [{ "command": "./b.sh" }],
                "stop": [{ "command": "./c.sh", "enabled": false }]
                """));

        var set = await LoadAsync(files);

        set.ActiveEvents().ShouldBe(["beforeReadFile", "afterFileEdit", "beforeShellExecution", "preToolUse"], ignoreOrder: true);
        set.FailClosedEvents().ShouldBe(["beforeReadFile", "beforeShellExecution"], ignoreOrder: true);
    }
}
