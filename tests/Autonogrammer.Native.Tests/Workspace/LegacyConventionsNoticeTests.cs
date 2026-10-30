using Autonogrammer.Native.Views.Workspace;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Workspace;

/// <summary>The conventions model remembers one notice per legacy root and moves only a user directory that does not collide.</summary>
public sealed class LegacyConventionsNoticeTests
{
    /// <summary>A second poll of the same tree adds no notice.</summary>
    [Fact]
    public void NoticesAreDeduplicatedForTheModelLifetime()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(LegacyBrandNames.ConventionDirectory + "/rules/style.md", "legacy");
        using var catalog = new WorkspaceCatalog(new WorkspaceRoot(workspace.Root));
        using var model = new WorkspaceConventionsModel(catalog);

        model.Notices.Count.ShouldBe(1);
        model.Notices.Count.ShouldBe(1);
        catalog.Reload();
        model.Notices.Count.ShouldBe(1);
    }

    /// <summary>The user-scope move runs only when the current directory is absent, and refuses a collision without merging.</summary>
    [Fact]
    public void UserMoveRefusesACollisionAndMovesWhenTheNewDirectoryIsAbsent()
    {
        var home = Directory.CreateTempSubdirectory("legacy-move-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(home, LegacyBrandNames.ConventionDirectory));
            Directory.CreateDirectory(Path.Combine(home, ".autonogrammer"));
            using var workspace = new TempWorkspace();
            using var catalog = new WorkspaceCatalog(new WorkspaceRoot(workspace.Root));
            using var model = new WorkspaceConventionsModel(catalog);

            var conflict = model.MoveUserLegacyDirectory(home);
            conflict.Status.ShouldBe(LegacyMoveStatus.Conflict);
            conflict.Message.ShouldNotBeNull().ShouldContain(conflict.LegacyPath);
            conflict.Message.ShouldContain(conflict.NewPath);
            Directory.Exists(Path.Combine(home, LegacyBrandNames.ConventionDirectory)).ShouldBeTrue();

            Directory.Delete(Path.Combine(home, ".autonogrammer"));
            model.MoveUserLegacyDirectory(home).Status.ShouldBe(LegacyMoveStatus.Moved);
            Directory.Exists(Path.Combine(home, ".autonogrammer")).ShouldBeTrue();
            Directory.Exists(Path.Combine(home, LegacyBrandNames.ConventionDirectory)).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>The repository command is the documented git mv text and is copied, never executed.</summary>
    [Fact]
    public async Task RepoScopeCopiesTheDocumentedCommand()
    {
        var ct = TestContext.Current.CancellationToken;
        using var workspace = new TempWorkspace();
        workspace.Write(LegacyBrandNames.ConventionDirectory + "/rules/style.md", "legacy");
        using var catalog = new WorkspaceCatalog(new WorkspaceRoot(workspace.Root));
        using var model = new WorkspaceConventionsModel(catalog);
        model.RepoMoveCommand.ShouldStartWith("git mv");
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var view = new WorkspaceConventionsView { DataContext = model };
            ViewHost.Show(view);
            view.NoticeRows.Items.Count.ShouldBe(1);
            view.NoticeRows.ItemCount.ShouldBe(1);
            var button = view.NoticeRows.Items.Cast<object>().ShouldHaveSingleItem();
            button.ShouldBeOfType<LegacyConventionNoticeRow>().Name.ShouldContain(LegacyBrandNames.ConventionDirectory);
            _ = view.CopyRepoCommandAsync(model.RepoMoveCommand);
            view.LastCopiedCommand.ShouldBe(model.RepoMoveCommand);
        }, ct).ConfigureAwait(true);
    }
}
