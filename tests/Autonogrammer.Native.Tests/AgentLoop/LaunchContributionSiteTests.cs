using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Native.Agents;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// Pins the Native application site for Phase 82 Task 2 as fixed by Task 7: <see cref="SessionEngineSet"/> applies launch contributors in
/// <c>BuildPlanAsync</c> right after the planner, before the overlay file is finalised, so contributed overlay entries (BundleLaunchContributor's
/// <c>skills.customDirectories=</c> entries) reach the single <c>ConfigOverlayWriter.Write</c> call. Arguments and environment merge into the
/// planned launch once, the wrapper wins, and the prompt paragraph/first-prompt block reach no argv.
/// </summary>
public sealed class LaunchContributionSiteTests
{
    /// <summary>
    /// A host-registered contributor: its arguments, environment, overlay entries and wrapper reach the planned launch (once) and the
    /// composed engine, and the overlay file carries the entries.
    /// </summary>
    [Fact]
    public async Task NativeContributorsMergeArgumentsEnvironmentOverlayEntriesAndWrapperExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        var omp = new ScriptedOmp();
        var head = await AgentHead.CreateAsync(Options(tree, omp)).ConfigureAwait(true);
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);

        var wrapper = new PassthroughWrapper();
        head.Host.RegisterLaunchContributor(new AllFieldsContributor(wrapper));

        var (id, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var plan = await slot.PlanAsync(ct).ConfigureAwait(true);

        // The planner never saw the host-registered contributor; BuildPlanAsync applied it to the planned launch.
        plan.Launch.ExtraArguments.Count(static argument => string.Equals(argument, "--native-contrib", StringComparison.Ordinal)).ShouldBe(1);
        plan.Launch.Environment["NATIVE_CONTRIB"].ShouldBe("1");
        plan.Launch.ConfigOverlayPath.ShouldNotBeNull();
        plan.Launch.OverlayEntries.ShouldContain("skills.customDirectories=/native-skills", StringComparer.Ordinal);

        await SendAsync(slot, "hi", ct).ConfigureAwait(true);

        AcpClientOptions launch = omp.Launches.Single();
        launch.ExtraArguments.ShouldContain("--native-contrib", StringComparer.Ordinal);
        launch.EnvironmentVariables["NATIVE_CONTRIB"].ShouldBe("1");
        launch.ProcessWrapper.ShouldBeSameAs(wrapper);
        string.Join('\n', launch.ExtraArguments).ShouldNotContain("PARAGRAPH-MARKER", Case.Sensitive);
        string.Join('\n', launch.ExtraArguments).ShouldNotContain("FIRSTPROMPT-MARKER", Case.Sensitive);

        // Overlay entries reach the written overlay file the plan names.
        (await File.ReadAllTextAsync(plan.Launch.ConfigOverlayPath!, ct).ConfigureAwait(true)).ShouldContain("skills.customDirectories=/native-skills", Case.Sensitive);
        launch.ExtraArguments.Count(static argument => string.Equals(argument, "--native-contrib", StringComparison.Ordinal)).ShouldBe(1);

        _ = id;
    }

    /// <summary>
    /// P4/pstack pin: bundle skill roots come only from the roots BundleComposition is actually
    /// configured with — the single plugins root — so skill directories from the ultrathink submodule tree (the
    /// <c>pstack</c> surface) cannot appear through bundle roots unless installed under that root.
    /// </summary>
    [Fact]
    public void BundleSkillRootsComeOnlyFromTheConfiguredPluginsRoot()
    {
        using var tree = new TempTree();
        tree.Write("plugins/my-bundle/skills/pstack/SKILL.md", "# pstack\n");
        tree.Write("plugins/my-bundle/skills/other/SKILL.md", "# other\n");
        var composition = new Autonogrammer.Native.Composition.Plugins.BundleComposition(tree.Plugins, tree.State);
        composition.PluginsRoot.ShouldBe(tree.Plugins);

        var contribution = composition.Contributor.Contribute(
            new LaunchRequest { Surface = "desktop", SessionId = "s1", WorkingDirectory = tree.Workspace, TrustedFolders = [] },
            TestContext.Current.CancellationToken);

        composition.Contributor.EnabledSkillRoots.ShouldBe(
            [
                Path.Combine(tree.Plugins, "my-bundle", "skills"),
            ]);
        contribution.OverlayEntries.ShouldContain(
            Autonogrammer.Harness.PluginBundles.BundleLaunchContributor.OverlayPrefix
            + Path.Combine(tree.Plugins, "my-bundle", "skills"),
            StringComparer.Ordinal);
        composition.Contributor.EnabledSkillRoots.ShouldAllBe(
            root => root.StartsWith(tree.Plugins + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    private sealed class AllFieldsContributor(IProcessLaunchWrapper wrapper) : ILaunchContributor
    {
        public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken) => new()
        {
            Arguments = ["--native-contrib", "value"],
            Environment = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["NATIVE_CONTRIB"] = "1",
            },
            OverlayEntries = ["skills.customDirectories=/native-skills"],
            SystemPromptParagraph = "PARAGRAPH-MARKER",
            FirstPromptBlock = "FIRSTPROMPT-MARKER",
            ProcessWrapper = wrapper,
        };
    }

    private sealed class PassthroughWrapper : IProcessLaunchWrapper
    {
        public ProcessLaunch Wrap(ProcessLaunch intended) => intended;
    }
}
