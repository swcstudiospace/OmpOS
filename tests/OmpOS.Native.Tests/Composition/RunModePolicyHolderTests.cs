using OmpOS.Harness.AgentLoop.Safety;
using OmpOS.Native.Composition;
using OmpOS.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Composition;

/// <summary>The run mode policy of a workspace follows the saved mode and the permission files, and never throws on a bad file.</summary>
public sealed class RunModePolicyHolderTests
{
    [Fact]
    public async Task TheDefaultModeIsAutoReviewAndASavedModeReachesEveryHolder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var hub = new RunModeHub(tree.Root, new FileRunModeStore(tree.Root), static _ => true);
        using var holder = hub.Open(tree.Workspace);
        await holder.RefreshAsync(ct).ConfigureAwait(true);
        holder.Mode.ShouldBe(RunMode.AutoReview);

        (await hub.Store.SaveAsync(RunMode.RunEverything, ct).ConfigureAwait(true)).ShouldNotBeNull();
        await hub.RefreshAllAsync(ct).ConfigureAwait(true);

        holder.Mode.ShouldBe(RunMode.RunEverything);
        holder.Launch.ApprovalMode.ShouldBe("yolo");
    }

    [Fact]
    public async Task AShellDenyRuleInTheUserFileProducesTheBashOverlayWithoutARefresh()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var hub = new RunModeHub(tree.Root, new FileRunModeStore(tree.Root), static _ => true);
        using var holder = hub.Open(tree.Workspace);
        await holder.RefreshAsync(ct).ConfigureAwait(true);
        holder.BashOverlayYaml.ShouldBeNull();
        var file = SafetyFileLocations.Permissions(tree.Root, null).First(static location => location.Path.EndsWith(SafetyFileLocations.PermissionsFileName, StringComparison.Ordinal)).Path;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "{\"deny\":[\"Shell(rm)\"]}");

        _ = holder.Current;
        await holder.RefreshAsync(ct).ConfigureAwait(true);

        holder.BashOverlayYaml.ShouldNotBeNull();
    }

    [Fact]
    public async Task AnInvalidPermissionFileNeverThrowsAndKeepsTheDefaults()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var file = SafetyFileLocations.Permissions(tree.Root, null).First().Path;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "{ not json");
        var hub = new RunModeHub(tree.Root, new FileRunModeStore(tree.Root), static _ => true);
        using var holder = hub.Open(tree.Workspace);

        await holder.RefreshAsync(ct).ConfigureAwait(true);

        holder.Mode.ShouldBe(RunMode.AutoReview);
        holder.Current.Mode.ShouldBe(RunMode.AutoReview);
    }
}
