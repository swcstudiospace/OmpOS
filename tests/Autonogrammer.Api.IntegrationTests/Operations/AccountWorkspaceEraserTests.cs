using Autonogrammer.Api.IntegrationTests.WorkspaceSettings;
using Autonogrammer.Api.Operations;
using Autonogrammer.Api.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Operations;

/// <summary>
/// The workspace erasure guard. Only the account's own folder is deleted, and a base that is not absolute, a folder that is not strictly under the base, and a
/// folder that is a symbolic link are refused without deleting anything. Links inside the folder are removed as entries and never followed.
/// </summary>
public sealed class AccountWorkspaceEraserTests
{
    private const string Alice = "acct-alice";
    private const string Bob = "acct-bob";

    /// <summary>Verifies a null base is a no-op that does not throw.</summary>
    [Fact]
    public async Task NullBase_IsANoOp()
    {
        var ct = TestContext.Current.CancellationToken;

        await AccountWorkspaceEraser.DeleteAsync(null, Alice, ct).ConfigureAwait(true);
    }

    /// <summary>Verifies erasure deletes only the account's folder and leaves a sibling account's folder intact.</summary>
    [Fact]
    public async Task Erase_DeletesOnlyTheAccountFolder_AndLeavesASiblingIntact()
    {
        var ct = TestContext.Current.CancellationToken;
        using var workspace = new TempWorkspace();
        var aliceFolder = Path.Combine(workspace.Root, AccountWorkspace.DirectoryName(Alice));
        var bobFolder = Path.Combine(workspace.Root, AccountWorkspace.DirectoryName(Bob));
        _ = workspace.Write("notes/a.txt", "alice", aliceFolder);
        _ = workspace.Write("b.txt", "bob", bobFolder);

        await AccountWorkspaceEraser.DeleteAsync(workspace.Root, Alice, ct).ConfigureAwait(true);

        Directory.Exists(aliceFolder).ShouldBeFalse();
        (await File.ReadAllTextAsync(Path.Combine(bobFolder, "b.txt"), ct).ConfigureAwait(true)).ShouldBe("bob");
    }

    /// <summary>Verifies a missing account folder is a no-op that does not throw and leaves the base in place.</summary>
    [Fact]
    public async Task MissingAccountFolder_IsANoOp()
    {
        var ct = TestContext.Current.CancellationToken;
        using var workspace = new TempWorkspace();

        await AccountWorkspaceEraser.DeleteAsync(workspace.Root, Alice, ct).ConfigureAwait(true);

        Directory.Exists(workspace.Root).ShouldBeTrue();
        Directory.Exists(Path.Combine(workspace.Root, AccountWorkspace.DirectoryName(Alice))).ShouldBeFalse();
    }

    /// <summary>Verifies a relative base is refused with an exception.</summary>
    [Fact]
    public async Task RelativeBase_Throws()
    {
        var ct = TestContext.Current.CancellationToken;

        _ = await Should.ThrowAsync<InvalidOperationException>(() => AccountWorkspaceEraser.DeleteAsync(Path.Combine("relative", "ws"), Alice, ct))
            .ConfigureAwait(true);
    }

    /// <summary>Verifies a symbolic link at the account folder is refused, and the link's target is left intact.</summary>
    [Fact]
    public async Task SymlinkAtAccountFolder_IsRefused_AndTargetSurvives()
    {
        var ct = TestContext.Current.CancellationToken;
        using var workspace = new TempWorkspace();
        var outside = workspace.NewFolder();
        _ = workspace.Write("keep.txt", "outside", outside);
        var accountName = AccountWorkspace.DirectoryName(Alice);
        if (!workspace.TryLink(accountName, outside))
        {
            Assert.Skip("This host cannot create symbolic links.");
        }

        _ = await Should.ThrowAsync<InvalidOperationException>(() => AccountWorkspaceEraser.DeleteAsync(workspace.Root, Alice, ct))
            .ConfigureAwait(true);
        (
                await File.ReadAllTextAsync(Path.Combine(outside, "keep.txt"), ct).ConfigureAwait(true)).ShouldBe("outside");
        new DirectoryInfo(Path.Combine(workspace.Root, accountName)).LinkTarget.ShouldNotBeNull();
    }

    /// <summary>Verifies a link inside the account folder that points outside the base is removed as an entry, and the outside files survive.</summary>
    [Fact]
    public async Task LinkInsideAccountFolder_IsRemovedAsAnEntry_AndOutsideFilesSurvive()
    {
        var ct = TestContext.Current.CancellationToken;
        using var workspace = new TempWorkspace();
        var accountName = AccountWorkspace.DirectoryName(Alice);
        var accountFolder = Path.Combine(workspace.Root, accountName);
        _ = workspace.Write("own.txt", "own", accountFolder);
        var outside = workspace.NewFolder();
        _ = workspace.Write("secret.txt", "secret", outside);
        if (!workspace.TryLink($"{accountName}/outside", outside))
        {
            Assert.Skip("This host cannot create symbolic links.");
        }

        await AccountWorkspaceEraser.DeleteAsync(workspace.Root, Alice, ct).ConfigureAwait(true);
        (
                await File.ReadAllTextAsync(Path.Combine(outside, "secret.txt"), ct).ConfigureAwait(true)).ShouldBe("secret");
        Directory.Exists(accountFolder).ShouldBeFalse();
    }
}
