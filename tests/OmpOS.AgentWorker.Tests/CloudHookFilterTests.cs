using OmpOS.AgentWorker.Hooks;
using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.AgentWorker.Tests;

/// <summary>Which of the hooks found for a workspace a cloud worker runs: project command hooks of the fourteen cloud events, nothing else.</summary>
public sealed class CloudHookFilterTests
{
    private const string ProjectHooks = """
        {"version":1,"hooks":{
          "beforeShellExecution":[{"command":"true"}],
          "afterFileEdit":[{"command":"true"}],
          "sessionStart":[{"command":"true"}],
          "sessionEnd":[{"command":"true"}],
          "workspaceOpen":[{"command":"true"}],
          "beforeMCPExecution":[{"command":"true"}],
          "afterMCPExecution":[{"command":"true"}],
          "beforeTabFileRead":[{"command":"true"}],
          "afterTabFileEdit":[{"command":"true"}],
          "stop":[{"type":"prompt","prompt":"Is the work done?"}],
          "preToolUse":[{"command":"true","runsIn":["local"]}]
        }}
        """;

    private static string NewDirectory() => Directory.CreateTempSubdirectory("ompos-hook-filter-").FullName;

    private static async Task<EffectiveHookSet> LoadAsync(string project, string? home, bool remote, CancellationToken cancellationToken)
    {
        var context = new HookLocatorContext([project], home, HookPlatforms.Current, IsRemote: remote);
        return await new HooksConfigLoader(new LocalHookFileSource()).LoadAsync(context, null, cancellationToken).ConfigureAwait(true);
    }

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>The cloud runs exactly the fourteen events Cursor runs in cloud agents; the lifecycle, MCP and Tab events are the other seven.</summary>
    [Fact]
    public void TheFourteenCloudEventsAreExactlyTheSupportedOnes()
    {
        var all = Enum.GetValues<HookEvent>();

        CloudHookEvents.Supported.Count.ShouldBe(14);
        all.Count(CloudHookEvents.IsSupported).ShouldBe(14);
        all.Where(static hookEvent => !CloudHookEvents.IsSupported(hookEvent)).Select(HookEvents.Format).Order(StringComparer.Ordinal).ToArray().ShouldBe(
            ["afterMCPExecution", "afterTabFileEdit", "beforeMCPExecution", "beforeTabFileRead", "sessionEnd", "sessionStart", "workspaceOpen"]);
    }

    /// <summary>Hooks of the lifecycle, MCP and Tab events, prompt hooks and hooks that opted out of remote runs are dropped; the rest stays.</summary>
    [Fact]
    public async Task OnlyCommandHooksOfTheCloudEventsThatRunRemotelyRemain()
    {
        var project = NewDirectory();
        try
        {
            Write(project, ".cursor/hooks.json", ProjectHooks);
            var all = await LoadAsync(project, home: null, remote: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
            all.Hooks.Count.ShouldBeGreaterThan(CloudHookFilter.Apply(all).Hooks.Count);

            var kept = CloudHookFilter.Apply(all);

            kept.Hooks.Select(static hook => hook.EventName).Order(StringComparer.Ordinal).ToArray().ShouldBe(["afterFileEdit", "beforeShellExecution"]);
            kept.Hooks.ShouldAllBe(static hook => hook.Type == HookType.Command && hook.Scope == HookScope.Project);
        }
        finally
        {
            Directory.Delete(project, recursive: true);
        }
    }

    /// <summary>The user's own hook files do not apply in the cloud: a set that holds them (loaded on a desktop) loses them, and a remote load never finds them.</summary>
    [Fact]
    public async Task UserScopeHooksAreIgnored()
    {
        var project = NewDirectory();
        var home = NewDirectory();
        try
        {
            Write(project, ".cursor/hooks.json", "{\"version\":1,\"hooks\":{\"stop\":[{\"command\":\"true\"}]}}");
            Write(home, ".cursor/hooks.json", "{\"version\":1,\"hooks\":{\"beforeShellExecution\":[{\"command\":\"true\"}]}}");
            Write(home, ".claude/settings.json", "{\"hooks\":{\"PreToolUse\":[{\"matcher\":\"Bash\",\"hooks\":[{\"type\":\"command\",\"command\":\"true\"}]}]}}");
            var token = TestContext.Current.CancellationToken;

            var desktop = await LoadAsync(project, home, remote: false, token).ConfigureAwait(true);
            var cloud = await LoadAsync(project, home, remote: true, token).ConfigureAwait(true);

            desktop.Hooks.Any(static hook => hook.Scope is HookScope.User or HookScope.ClaudeUser).ShouldBeTrue("the desktop load must find the user's files for this test to prove anything");
            CloudHookFilter.Apply(desktop).Hooks.ShouldAllBe(static hook => hook.Scope == HookScope.Project);
            CloudHookFilter.Apply(desktop).Hooks.Select(static hook => hook.EventName).ToArray().ShouldBe(["stop"]);
            CloudHookFilter.Apply(cloud).Hooks.Select(static hook => hook.EventName).ToArray().ShouldBe(["stop"]);
            CloudHookFilter.Apply(desktop).Sources.ShouldAllBe(static source => source.Scope == HookScope.Project);
        }
        finally
        {
            Directory.Delete(project, recursive: true);
            Directory.Delete(home, recursive: true);
        }
    }
}
